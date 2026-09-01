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

/// <summary>親 Cancel カスケードと分岐 Wait の子配送解決。</summary>
public sealed class ForkChildExecutionCoordinatorCancelAndDeliveryTests
{
    /// <summary>親 Cancel カスケードで Running 子だけに Cancel work item を enqueue する。</summary>
    [Fact]
    public async Task CascadeCancelToRunningChildrenAsync_EnqueuesCancelOnlyForRunningChildren()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childRunning = Guid.NewGuid();
        var childCompleted = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childRunning, childCompleted, now);
        await MarkBranchStatusAsync(
            db.Options, parentId, childCompleted, ExecutionBranchStatuses.Completed, now);

        var workQueue = new CapturingWorkQueue();
        var sut = CreateSut(db, workQueue);

        // Act
        await sut.CascadeCancelToRunningChildrenAsync(parentId, CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(ExecutionWorkItemKinds.Cancel, workQueue.Items[0].Kind);
        Assert.Equal(childRunning, workQueue.Items[0].ExecutionId);
    }

    /// <summary>親自身に Wait があるときは子へ振り替えない。</summary>
    [Fact]
    public async Task TryResolveChildWaitDeliveryAsync_WhenParentHasWait_ReturnsNull()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childId, Guid.NewGuid(), now);
        await SeedWaitAsync(db.Options, parentId, "ParentWait", "Approve", now);
        await SeedWaitAsync(db.Options, childId, "ChildWait", "Approve", now);
        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var delivery = await sut.TryResolveChildWaitDeliveryAsync(
            parentId, "ParentWait", "Approve", CancellationToken.None);

        // Assert
        Assert.Null(delivery);
    }

    /// <summary>親に Wait が無く子にだけあるとき、子 execution へ振り替える。</summary>
    [Fact]
    public async Task TryResolveChildWaitDeliveryAsync_WhenOnlyChildHasWait_ReturnsChildTarget()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childId, siblingId, now);
        await SeedWaitAsync(db.Options, childId, "BranchWait", "Approve", now);
        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var delivery = await sut.TryResolveChildWaitDeliveryAsync(
            parentId, "BranchWait", "Approve", CancellationToken.None);

        // Assert
        Assert.NotNull(delivery);
        Assert.Equal(childId, delivery.ExecutionId);
        Assert.Equal("BranchWait", delivery.NodeId);
        Assert.Equal("Approve", delivery.EventName);
    }

    /// <summary>PublishEvent 互換（nodeId なし）でも子 Wait へ解決する。</summary>
    [Fact]
    public async Task TryResolveChildWaitDeliveryAsync_WithoutNodeId_ResolvesChildByEventName()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childId, Guid.NewGuid(), now);
        await SeedWaitAsync(db.Options, childId, "BranchWait", "Approve", now);
        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var delivery = await sut.TryResolveChildWaitDeliveryAsync(
            parentId, nodeId: null, "Approve", CancellationToken.None);

        // Assert
        Assert.NotNull(delivery);
        Assert.Equal(childId, delivery.ExecutionId);
        Assert.Equal("BranchWait", delivery.NodeId);
    }

    /// <summary>複数子に同一イベント Wait があるとき曖昧として例外になる。</summary>
    [Fact]
    public async Task TryResolveChildWaitDeliveryAsync_WhenAmbiguous_Throws()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedParentAndBranchesAsync(db.Options, parentId, childA, childB, now);
        await SeedWaitAsync(db.Options, childA, "WaitA", "Approve", now);
        await SeedWaitAsync(db.Options, childB, "WaitB", "Approve", now);
        var sut = CreateSut(db, new CapturingWorkQueue());

        // Act
        var act = () => sut.TryResolveChildWaitDeliveryAsync(
            parentId, nodeId: null, "Approve", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    private static ForkChildExecutionCoordinator CreateSut(InMemoryTestDatabase db, IExecutionWorkQueue workQueue)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executor = new TestCoreTransactionExecutor(uowFactory);
        return new ForkChildExecutionCoordinator(
            executor,
            new ExecutionRepository(),
            new ExecutionBranchRepository(),
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
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
        await using var ctx = new CoreDbContext(options);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = parentId,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = ExecutionProjectionStatuses.Running,
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });

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

    private static async Task MarkBranchStatusAsync(
        DbContextOptions<CoreDbContext> options,
        Guid parentId,
        Guid childId,
        string status,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        var row = await ctx.ExecutionBranches.SingleAsync(
            x => x.ParentExecutionId == parentId && x.ExecutionId == childId);
        row.Status = status;
        row.UpdatedAt = now;
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedWaitAsync(
        DbContextOptions<CoreDbContext> options,
        Guid executionId,
        string nodeId,
        string eventName,
        DateTime now)
    {
        await using var ctx = new CoreDbContext(options);
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = [eventName],
            CreatedAt = now
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
        public Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default) =>
            Task.FromResult(uuid.ToString("D"));
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
}
