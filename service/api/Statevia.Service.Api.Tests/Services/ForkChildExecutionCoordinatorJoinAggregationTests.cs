using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>子終端信号と Join 再評価（タスク 4）。</summary>
public sealed class ForkChildExecutionCoordinatorJoinAggregationTests
{
    /// <summary>子終端で branch status を更新し親へ ChildCompleted Resume を enqueue する。</summary>
    [Fact]
    public async Task NotifyChildTerminalAsync_UpdatesBranchAndEnqueuesParentResume()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childA, childB, now);

        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue);

        // Act
        var signaled = await sut.NotifyChildTerminalAsync(
            childA,
            ExecutionProjectionStatuses.Completed,
            """{"v":1}""",
            CancellationToken.None);

        // Assert
        Assert.True(signaled);
        Assert.Single(workQueue.Items);
        Assert.Equal(ExecutionWorkItemKinds.Resume, workQueue.Items[0].Kind);
        Assert.Equal(parentId, workQueue.Items[0].ExecutionId);
        Assert.Contains(ExecutionWaitEventNames.ChildCompleted, workQueue.Items[0].Payload, StringComparison.Ordinal);
        Assert.Contains("fork-node-1", workQueue.Items[0].Payload, StringComparison.Ordinal);

        await using var verify = new CoreDbContext(db.Options);
        var row = await verify.ExecutionBranches.SingleAsync(x => x.ExecutionId == childA);
        Assert.Equal(ExecutionBranchStatuses.Completed, row.Status);
        Assert.Equal("""{"v":1}""", row.OutputJson);
    }

    /// <summary>全子 Completed なら候補 input 付き Satisfied を返す。</summary>
    [Fact]
    public async Task EvaluateJoinAsync_WhenAllCompleted_ReturnsSatisfiedWithCandidateInputs()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childA, childB, now);
        await MarkBranchAsync(db.Options, parentId, "A", ExecutionBranchStatuses.Completed, """{"a":true}""", now);
        await MarkBranchAsync(db.Options, parentId, "B", ExecutionBranchStatuses.Completed, """{"b":true}""", now);

        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var evaluation = await sut.EvaluateJoinAsync(parentId, "fork-node-1", CancellationToken.None);

        // Assert
        Assert.Equal(ForkJoinEvaluationKind.Satisfied, evaluation.Kind);
        Assert.Equal("Join1", evaluation.JoinState);
        Assert.NotNull(evaluation.CandidateInputs);
        Assert.Equal(2, evaluation.CandidateInputs.Count);
        Assert.True(evaluation.CandidateInputs.ContainsKey("A"));
        Assert.True(evaluation.CandidateInputs.ContainsKey("B"));
        Assert.Equal(JsonValueKind.Object, ((JsonElement)evaluation.CandidateInputs["A"]!).ValueKind);
    }

    /// <summary>一文 Failed なら Failed 評価を返す。</summary>
    [Fact]
    public async Task EvaluateJoinAsync_WhenOneFailed_ReturnsFailed()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childA, childB, now);
        await MarkBranchAsync(db.Options, parentId, "A", ExecutionBranchStatuses.Failed, null, now);

        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var evaluation = await sut.EvaluateJoinAsync(parentId, "fork-node-1", CancellationToken.None);

        // Assert
        Assert.Equal(ForkJoinEvaluationKind.Failed, evaluation.Kind);
        Assert.Equal(ExecutionBranchStatuses.Failed, evaluation.FailureStatus);
        Assert.Null(evaluation.CandidateInputs);
    }

    /// <summary>未終端子が残るあいだは Waiting。</summary>
    [Fact]
    public async Task EvaluateJoinAsync_WhenStillRunning_ReturnsWaiting()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childA, childB, now);
        await MarkBranchAsync(db.Options, parentId, "A", ExecutionBranchStatuses.Completed, "{}", now);

        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var evaluation = await sut.EvaluateJoinAsync(parentId, "fork-node-1", CancellationToken.None);

        // Assert
        Assert.Equal(ForkJoinEvaluationKind.Waiting, evaluation.Kind);
    }

    private static ForkChildExecutionCoordinator CreateSut(InMemoryTestDatabase db, IExecutionWorkQueue workQueue)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executor = new TestCoreTransactionExecutor(uowFactory);
        return new ForkChildExecutionCoordinator(
            executor,
            new ExecutionRepository(),
            new ExecutionBranchRepository(),
            workQueue,
            new GuidIdGenerator(),
            new StubDisplayIdWrites(),
            new EventStoreRepository(new GuidIdGenerator()),
            Options.Create(new ForkChildExpansionOptions { MaxAttempts = 1, BaseDelayMs = 0 }),
            NullLogger<ForkChildExecutionCoordinator>.Instance);
    }

    private static async Task SeedParentAndBranchesAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childA,
        Guid childB,
        DateTime now)
    {
        await using var db = new CoreDbContext(options);
        var tenantId = TestTenantIds.T1TenantId;
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        db.Executions.Add(new ExecutionRow
        {
            ExecutionId = parentId,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = definitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            SecuritySnapshotJson = "{}"
        });
        db.Executions.Add(new ExecutionRow
        {
            ExecutionId = childA,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = definitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            SecuritySnapshotJson = "{}"
        });
        db.Executions.Add(new ExecutionRow
        {
            ExecutionId = childB,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = definitionVersionId,
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            SecuritySnapshotJson = "{}"
        });
        db.ExecutionBranches.AddRange(
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
        await db.SaveChangesAsync();
    }

    private static async Task MarkBranchAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        string branchState,
        string status,
        string? outputJson,
        DateTime now)
    {
        await using var db = new CoreDbContext(options);
        var row = await db.ExecutionBranches.SingleAsync(x =>
            x.ParentExecutionId == parentId && x.BranchState == branchState);
        row.Status = status;
        row.OutputJson = outputJson;
        row.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    private sealed class GuidIdGenerator : IIdGenerator
    {
        public Guid NewGuid() => Guid.NewGuid();
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
}
