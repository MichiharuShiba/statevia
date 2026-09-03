using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ForkChildExecutionCoordinator.ExpandForkAsync"/> の展開・再入・D9 失敗経路。</summary>
public sealed class ForkChildExecutionCoordinatorExpandTests
{
    /// <summary>初回展開で子 execution・branches・Start work item を同一 TX で作成する。</summary>
    [Fact]
    public async Task ExpandForkAsync_CreatesChildrenBranchesAndStartWorkItems()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAsync(db.Options, parentId, definitionId, definitionVersionId, now);

        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue, new ForkChildExpansionOptions { MaxAttempts = 2, BaseDelayMs = 0 });
        var request = CreateRequest(parentId, definitionId, definitionVersionId);

        // Act
        var result = await sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Join1", result.JoinState);
        Assert.Equal(2, result.ChildExecutionIds.Count);
        Assert.Equal(2, workQueue.Items.Count);
        Assert.All(workQueue.Items, item => Assert.Equal(ExecutionWorkItemKinds.Start, item.Kind));

        await using var verify = new CoreDbContext(db.Options);
        var branches = await verify.ExecutionBranches
            .Where(x => x.ParentExecutionId == parentId)
            .OrderBy(x => x.BranchState)
            .ToListAsync();
        Assert.Equal(2, branches.Count);
        Assert.Equal("A", branches[0].BranchState);
        Assert.Equal("B", branches[1].BranchState);
        Assert.Equal(2, await verify.Executions.CountAsync(x => x.ExecutionId != parentId));
        Assert.Equal(2, await verify.EventStore.CountAsync(x => x.Type == EventStoreEventType.WorkflowStarted.ToPersistedString()));
    }

    /// <summary>同数の branches が既にある再入では Start を二重 enqueue しない。</summary>
    [Fact]
    public async Task ExpandForkAsync_WhenAlreadyExpanded_DoesNotReEnqueueStarts()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAsync(db.Options, parentId, definitionId, definitionVersionId, now);
        await SeedChildAndBranchesAsync(db.Options, parentId, childA, childB, now);

        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue, new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 });
        var request = CreateRequest(parentId, definitionId, definitionVersionId);

        // Act
        var result = await sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { childA, childB }.OrderBy(x => x).ToArray(), result.ChildExecutionIds.OrderBy(x => x).ToArray());
        Assert.Empty(workQueue.Items);
    }

    /// <summary>展開が上限まで失敗すると親を Failed にし子 Cancel を enqueue する。</summary>
    [Fact]
    public async Task ExpandForkAsync_WhenAttemptsExhausted_FailsParentAndEnqueuesCancel()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAsync(db.Options, parentId, definitionId, definitionVersionId, now);
        await SeedPartialChildAsync(db.Options, parentId, childA, now);

        var workQueue = new CapturingWorkQueue();
        var failingBranches = new ThrowingBranchRepository();
        var sut = CreateSut(
            db,
            workQueue,
            new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 },
            branches: failingBranches);

        var request = CreateRequest(parentId, definitionId, definitionVersionId);

        // Act
        var result = await sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(result.ChildExecutionIds);

        await using var verify = new CoreDbContext(db.Options);
        var parent = await verify.Executions.SingleAsync(x => x.ExecutionId == parentId);
        Assert.Equal(ExecutionProjectionStatuses.Failed, parent.Status);
        Assert.True(parent.CancelRequested);

        Assert.Single(workQueue.Items);
        Assert.Equal(ExecutionWorkItemKinds.Cancel, workQueue.Items[0].Kind);
        Assert.Equal(childA, workQueue.Items[0].ExecutionId);
    }

    /// <summary>分岐が空のとき ArgumentException になる。</summary>
    [Fact]
    public async Task ExpandForkAsync_EmptyBranches_Throws()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue, new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 });
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var request = new ForkExpansionRequest(
            ParentExecutionId: parentId,
            TenantId: TestTenantIds.T1TenantId,
            DefinitionId: definitionId,
            DefinitionVersionId: Guid.NewGuid(),
            DefinitionVersion: 1,
            DefinitionDisplayId: definitionId.ToString("D"),
            SecuritySnapshotJson: "{}",
            CompiledDefinition: CreateDefinition(),
            ForkNodeId: "fork-node-1",
            Branches: Array.Empty<ForkBranchExpansion>());

        // Act
        var act = () => sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    /// <summary>MaxAttempts が不正なとき InvalidOperationException になる。</summary>
    [Fact]
    public async Task ExpandForkAsync_InvalidMaxAttempts_Throws()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue, new ForkChildExpansionOptions { MaxAttempts = 0, BaseDelayMs = 0 });
        var parentId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        await SeedParentAsync(db.Options, parentId, definitionId, definitionVersionId, DateTime.UtcNow);
        var request = CreateRequest(parentId, definitionId, definitionVersionId);

        // Act
        var act = () => sut.ExpandForkAsync(request, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
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

    private static ForkExpansionRequest CreateRequest(
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
            CompiledDefinition: CreateDefinition(),
            ForkNodeId: "fork-node-1",
            Branches:
            [
                new ForkBranchExpansion("A", new { n = 1 }),
                new ForkBranchExpansion("B", new { n = 2 })
            ]);

    private static CompiledWorkflowDefinition CreateDefinition() =>
        new()
        {
            Name = "ForkExpand",
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

    private static async Task SeedParentAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid definitionId,
        Guid definitionVersionId,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = parentId,
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
            ExecutionId = parentId,
            GraphJson = """{"nodes":[],"edges":[]}""",
            UpdatedAt = now
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedChildAndBranchesAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childA,
        Guid childB,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        foreach (var childId in new[] { childA, childB })
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

        ctx.ExecutionBranches.AddRange(
            new ExecutionBranchRow
            {
                ParentExecutionId = parentId,
                ExecutionId = childA,
                ForkNodeId = "fork-node-1",
                JoinState = "Join1",
                BranchState = "A",
                Status = ExecutionBranchStatuses.Running,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ExecutionBranchRow
            {
                ParentExecutionId = parentId,
                ExecutionId = childB,
                ForkNodeId = "fork-node-1",
                JoinState = "Join1",
                BranchState = "B",
                Status = ExecutionBranchStatuses.Running,
                CreatedAt = now,
                UpdatedAt = now
            });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedPartialChildAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childA,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = childA,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });
        ctx.ExecutionBranches.Add(new ExecutionBranchRow
        {
            ParentExecutionId = parentId,
            ExecutionId = childA,
            ForkNodeId = "fork-node-1",
            JoinState = "Join1",
            BranchState = "A",
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = now,
            UpdatedAt = now
        });
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
            IReadOnlyList<string>? kinds,
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

        public Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(DateTime nowUtc, int limit, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// List は実データを返し、Insert だけ失敗させて D9 上限経路を踏む。
    /// </summary>
    private sealed class ThrowingBranchRepository : IExecutionBranchRepository
    {
        private readonly ExecutionBranchRepository _inner = new();

        public Task InsertBranchesIdempotentAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionBranchRow> branches,
            CancellationToken ct) =>
            throw new InvalidOperationException("simulated insert failure");

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
