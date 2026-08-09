using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>
/// タスク 9: ネスト Fork の基本完走と展開の一時失敗リカバリ（循環耐久・幽霊枝は follow-up）。
/// </summary>
public sealed class ForkChildExecutionCoordinatorNestAndRecoveryTests
{
    /// <summary>展開の一時失敗後、リトライで子と Start が作成される（D9）。</summary>
    [Fact]
    public async Task ExpandForkAsync_WhenTransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db.Options, parentId, definitionId, definitionVersionId, now);

        var workQueue = new CapturingWorkQueue();
        var branches = new FailOnceThenSucceedBranchRepository();
        var sut = CreateSut(
            db,
            workQueue,
            new ForkChildExpansionOptions { MaxAttempts = 3, BaseDelayMs = 0 },
            branches);
        var request = CreateFlatExpandRequest(parentId, definitionId, definitionVersionId);

        // Act
        var result = await sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.ChildExecutionIds.Count);
        Assert.Equal(2, branches.InsertCalls);
        Assert.Equal(2, workQueue.Items.Count);
        Assert.All(workQueue.Items, item => Assert.Equal(ExecutionWorkItemKinds.Start, item.Kind));

        await using var verify = new CoreDbContext(db.Options);
        Assert.Equal(2, await verify.ExecutionBranches.CountAsync(x => x.ParentExecutionId == parentId));
        var parent = await verify.Executions.SingleAsync(x => x.ExecutionId == parentId);
        Assert.Equal(ExecutionProjectionStatuses.Running, parent.Status);
    }

    /// <summary>展開済み再入は Start を増やさず既存子 ID を返す（クラッシュ後 recovery）。</summary>
    [Fact]
    public async Task ExpandForkAsync_AfterCrashWithFullBranches_IsIdempotentRecovery()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db.Options, parentId, definitionId, definitionVersionId, now);
        await SeedBranchAsync(db.Options, parentId, childA, "fork-node-1", "Join1", "A", now);
        await SeedBranchAsync(db.Options, parentId, childB, "fork-node-1", "Join1", "B", now);

        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue, new ForkChildExpansionOptions { MaxAttempts = 2, BaseDelayMs = 0 });
        var request = CreateFlatExpandRequest(parentId, definitionId, definitionVersionId);

        // Act
        var result = await sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { childA, childB }.OrderBy(x => x).ToArray(),
            result.ChildExecutionIds.OrderBy(x => x).ToArray());
        Assert.Empty(workQueue.Items);
    }

    /// <summary>ネスト: 内側 Join 充足のあと外側 Join も充足できる（D4 基本経路）。</summary>
    [Fact]
    public async Task NestedFork_InnerThenOuterJoin_BothSatisfied()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var outerId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        var innerA = Guid.NewGuid();
        var innerB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();

        await SeedExecutionAsync(db.Options, outerId, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, midId, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, siblingId, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, innerA, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, innerB, definitionId, definitionVersionId, now);

        // 外側: OuterFast + Mid（内側 Fork 枝）
        await SeedBranchAsync(db.Options, outerId, siblingId, "outer-fork", "OuterJoin", "OuterFast", now);
        await SeedBranchAsync(db.Options, outerId, midId, "outer-fork", "OuterJoin", "InnerFork", now);
        // 内側: Mid が親
        await SeedBranchAsync(db.Options, midId, innerA, "inner-fork", "InnerJoin", "InnerA", now);
        await SeedBranchAsync(db.Options, midId, innerB, "inner-fork", "InnerJoin", "InnerB", now);

        await MarkBranchAsync(db.Options, midId, "InnerA", ExecutionBranchStatuses.Completed, """{"a":1}""", now.AddSeconds(1));
        await MarkBranchAsync(db.Options, midId, "InnerB", ExecutionBranchStatuses.Completed, """{"b":2}""", now.AddSeconds(2));
        await MarkBranchAsync(db.Options, outerId, "OuterFast", ExecutionBranchStatuses.Completed, """{"fast":true}""", now.AddSeconds(3));

        var sut = CreateSut(db, new CapturingWorkQueue(), new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 });

        // Act — 内側充足
        var innerEval = await sut.EvaluateJoinAsync(midId, "inner-fork", CancellationToken.None);
        Assert.Equal(ForkJoinEvaluationKind.Satisfied, innerEval.Kind);
        Assert.Equal("InnerJoin", innerEval.JoinState);

        // Mid 終端を外側へ通知
        var signaled = await sut.NotifyChildTerminalAsync(
            midId,
            ExecutionProjectionStatuses.Completed,
            """{"mid":true}""",
            """{"InnerFork":{"output":"merged"}}""",
            """{"k":1}""",
            CancellationToken.None);
        Assert.True(signaled);

        // Act — 外側充足
        var outerEval = await sut.EvaluateJoinAsync(outerId, "outer-fork", CancellationToken.None);

        // Assert
        Assert.Equal(ForkJoinEvaluationKind.Satisfied, outerEval.Kind);
        Assert.Equal("OuterJoin", outerEval.JoinState);
        Assert.NotNull(outerEval.CandidateInputs);
        Assert.True(outerEval.CandidateInputs.ContainsKey("OuterFast"));
        Assert.True(outerEval.CandidateInputs.ContainsKey("InnerFork"));
    }

    /// <summary>子孫列挙は孫まで再帰する（ネスト D4 / D6.1）。</summary>
    [Fact]
    public async Task ListDescendantExecutionIdsAsync_ReturnsNestedGrandchild()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var outerId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var leafId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        await SeedExecutionAsync(db.Options, outerId, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, midId, definitionId, definitionVersionId, now);
        await SeedExecutionAsync(db.Options, leafId, definitionId, definitionVersionId, now);
        await SeedBranchAsync(db.Options, outerId, midId, "outer-fork", "OuterJoin", "InnerFork", now);
        await SeedBranchAsync(db.Options, midId, leafId, "inner-fork", "InnerJoin", "InnerA", now);

        var sut = CreateSut(db, new CapturingWorkQueue(), new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 });

        // Act
        var ids = await sut.ListDescendantExecutionIdsAsync(outerId, CancellationToken.None);

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains(midId, ids);
        Assert.Contains(leafId, ids);
        Assert.DoesNotContain(outerId, ids);
    }

    private static ForkChildExecutionCoordinator CreateSut(
        InMemoryTestDatabase db,
        IExecutionWorkQueue workQueue,
        ForkChildExpansionOptions options,
        IExecutionBranchRepository? branches = null)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executor = new TestCoreTransactionExecutor(uowFactory);
        return new ForkChildExecutionCoordinator(
            executor,
            new ExecutionRepository(),
            branches ?? new ExecutionBranchRepository(),
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
            workQueue,
            new GuidIdGenerator(),
            new StubDisplayIdWrites(),
            new EventStoreRepository(new GuidIdGenerator()),
            Options.Create(options),
            NullLogger<ForkChildExecutionCoordinator>.Instance);
    }

    private static ForkExpansionRequest CreateFlatExpandRequest(
        Guid parentId,
        Guid definitionId,
        Guid definitionVersionId) =>
        new(
            ParentExecutionId: parentId,
            TenantId: TestTenantIds.T1TenantId,
            DefinitionId: definitionId,
            DefinitionVersionId: definitionVersionId,
            DefinitionVersion: 1,
            DefinitionDisplayId: definitionId.ToString("D"),
            SecuritySnapshotJson: """{"securityModelVersion":1}""",
            CompiledDefinition: CreateFlatDefinition(),
            ForkNodeId: "fork-node-1",
            Branches:
            [
                new ForkBranchExpansion("A", new { n = 1 }),
                new ForkBranchExpansion("B", new { n = 2 })
            ]);

    private static CompiledWorkflowDefinition CreateFlatDefinition() =>
        new()
        {
            Name = "NestRecoveryFlat",
            InitialState = "Start",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase),
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Start"] = ["A", "B"]
            },
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Join1"] = ["A", "B"]
            },
            WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
                StringComparer.OrdinalIgnoreCase),
            StateExecutorFactory = new DictionaryStateExecutorFactory(
                new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase))
        };

    private static async Task SeedExecutionAsync(
        DbContextOptions<CoreDbContext> options,
        Guid executionId,
        Guid definitionId,
        Guid definitionVersionId,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        if (await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            return;

        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = definitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false,
            SecuritySnapshotJson = """{"securityModelVersion":1}"""
        });
        ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
        {
            ExecutionId = executionId,
            GraphJson = """{"nodes":[],"edges":[]}""",
            UpdatedAt = now
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedBranchAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childId,
        string forkNodeId,
        string joinState,
        string branchState,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        if (!await ctx.Executions.AnyAsync(x => x.ExecutionId == childId))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = childId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = ExecutionProjectionStatuses.Running,
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
        }

        ctx.ExecutionBranches.Add(new ExecutionBranchRow
        {
            ParentExecutionId = parentId,
            ExecutionId = childId,
            ForkNodeId = forkNodeId,
            JoinState = joinState,
            BranchState = branchState,
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = now,
            UpdatedAt = now
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task MarkBranchAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        string branchState,
        string status,
        string? outputJson,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        var row = await ctx.ExecutionBranches.SingleAsync(x =>
            x.ParentExecutionId == parentId && x.BranchState == branchState);
        row.Status = status;
        row.OutputJson = outputJson;
        row.UpdatedAt = now;
        await ctx.SaveChangesAsync();
    }

    private sealed class GuidIdGenerator : IIdGenerator
    {
        public Guid NewSequentialGuid() => Guid.NewGuid();
        public Guid NewRandomGuid() => Guid.NewGuid();
    }

    private sealed class StubDisplayIdWrites : IDisplayIdWriteService
    {
        public Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default)
        {
            _ = uow;
            _ = kind;
            return Task.FromResult(uuid.ToString("D"));
        }
    }

    private sealed class CapturingWorkQueue : IExecutionWorkQueue
    {
        public List<ExecutionWorkItemRow> Items { get; } = [];

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            EnqueueAsync(item, ct);

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
        {
            Items.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            EnqueueManyAsync(items, ct);

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            Task.FromResult(0);

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            Task.FromResult(0);
    }

    /// <summary>1 回目の Insert だけ失敗させ、2 回目以降は実リポジトリへ委譲する。</summary>
    private sealed class FailOnceThenSucceedBranchRepository : IExecutionBranchRepository
    {
        private readonly ExecutionBranchRepository _inner = new();

        public int InsertCalls { get; private set; }

        public Task InsertBranchesIdempotentAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionBranchRow> branches,
            CancellationToken ct)
        {
            InsertCalls++;
            if (InsertCalls == 1)
                throw new InvalidOperationException("simulated transient insert failure");

            return _inner.InsertBranchesIdempotentAsync(uow, branches, ct);
        }

        public Task<IReadOnlyList<ExecutionBranchRow>> ListByParentAndForkNodeAsync(
            ICoreUnitOfWork uow,
            Guid parentExecutionId,
            string forkNodeId,
            CancellationToken ct) =>
            _inner.ListByParentAndForkNodeAsync(uow, parentExecutionId, forkNodeId, ct);

        public Task<IReadOnlyList<ExecutionBranchRow>> ListByParentExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid parentExecutionId,
            CancellationToken ct) =>
            _inner.ListByParentExecutionIdAsync(uow, parentExecutionId, ct);

        public Task<ExecutionBranchRow?> GetByChildExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid childExecutionId,
            CancellationToken ct) =>
            _inner.GetByChildExecutionIdAsync(uow, childExecutionId, ct);

        public Task<bool> TryUpdateStatusAsync(
            ICoreUnitOfWork uow,
            Guid parentExecutionId,
            string forkNodeId,
            string branchState,
            ExecutionBranchStatusUpdate update,
            CancellationToken ct) =>
            _inner.TryUpdateStatusAsync(uow, parentExecutionId, forkNodeId, branchState, update, ct);
    }
}
