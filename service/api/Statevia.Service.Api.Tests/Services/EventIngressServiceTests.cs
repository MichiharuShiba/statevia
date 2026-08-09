using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="EventIngressService"/> の集合配送シナリオ。</summary>
public sealed class EventIngressServiceTests
{
    /// <summary>一致購読ごとに Resume ワークを enqueue する。</summary>
    [Fact]
    public async Task PublishAsync_EnqueuesResumeForEachMatch()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(db, executionId, "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", now);

        // Act
        await service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        var item = workQueue.Items[0];
        Assert.Equal(executionId, item.ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, item.Kind);
        var payload = JsonSerializer.Deserialize<ExecutionResumeWorkItemPayload>(
            item.Payload,
            ExecutionWorkItemPayloadJson.Options);
        Assert.NotNull(payload);
        Assert.Equal(ExecutionResumeWorkItemModes.Event, payload.Mode);
        Assert.Equal("WaitNode", payload.NodeId);
        Assert.Equal("statevia.event.subscribe.0", payload.EventName);
    }

    /// <summary>key 省略・空白は空文字として照合する。</summary>
    [Fact]
    public async Task PublishAsync_NormalizesBlankKeyToEmptyString()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(
            db, executionId, "WaitNode", "orders.updated", "", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await service.PublishAsync("orders.updated", "   ", CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(executionId, workQueue.Items[0].ExecutionId);
    }

    /// <summary>一致 0 件でも例外なく完了し enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenNoMatch_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());

        // Act
        await service.PublishAsync("missing.topic", "", CancellationToken.None);

        // Assert
        Assert.Empty(workQueue.Items);
    }

    /// <summary>購読は子 execution_id に紐づき、親 ID へ Resume しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenSubscriptionOnChild_EnqueuesResumeForChildNotParent()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, parentId, now);
        await SeedSubscriptionAsync(db, childId, "ChildWait", "approval.needed", "req-1", "Approve", now);

        // Act
        await service.PublishAsync("approval.needed", "req-1", CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(childId, workQueue.Items[0].ExecutionId);
        Assert.NotEqual(parentId, workQueue.Items[0].ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, workQueue.Items[0].Kind);
    }

    /// <summary>購読が空 key のとき key 付き配信は一致しない。</summary>
    [Fact]
    public async Task PublishAsync_EmptyKeySubscription_DoesNotMatchNonEmptyKey()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        await SeedSubscriptionAsync(
            db, Guid.NewGuid(), "WaitNode", "orders.updated", "", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await service.PublishAsync("orders.updated", "order-1", CancellationToken.None);

        // Assert
        Assert.Empty(workQueue.Items);
    }

    private static async Task SeedSubscriptionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        string nodeId,
        string topic,
        string correlationKey,
        string resumeEventName,
        DateTime createdAt)
    {
        await SeedExecutionAsync(db, executionId, createdAt);
        await using var ctx = new CoreDbContext(db.Options);
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = [resumeEventName],
            CreatedAt = createdAt
        });
        ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
        {
            SubscriptionId = Guid.NewGuid(),
            ExecutionId = executionId,
            NodeId = nodeId,
            Topic = topic,
            CorrelationKey = correlationKey,
            ResumeEventName = resumeEventName,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedExecutionAsync(InMemoryTestDatabase db, Guid executionId, DateTime createdAt)
    {
        await using var ctx = new CoreDbContext(db.Options);
        if (await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            return;

        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = createdAt,
            UpdatedAt = createdAt,
            CancelRequested = false,
            RestartLost = false
        });
        await ctx.SaveChangesAsync();
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

        public Task ReleaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime availableAt,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredOwnershipRecoveriesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
