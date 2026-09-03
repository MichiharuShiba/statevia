using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json;

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
        var service = CreateService(transactions, waits, workQueue);
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
        var service = CreateService(transactions, waits, workQueue);
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
        var service = CreateService(transactions, waits, workQueue);

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
        var service = CreateService(transactions, waits, workQueue);
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, parentId, now, TestTenantIds.T1TenantId);
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
        var service = CreateService(transactions, waits, workQueue);
        await SeedSubscriptionAsync(
            db, Guid.NewGuid(), "WaitNode", "orders.updated", "", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await service.PublishAsync("orders.updated", "order-1", CancellationToken.None);

        // Assert
        Assert.Empty(workQueue.Items);
    }

    /// <summary>write なしは 403 相当で enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenWritePermissionMissing_ThrowsForbiddenAndDoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = CreateService(
            transactions,
            waits,
            workQueue,
            new DenyingRuntimePermissionAuthorization());
        await SeedSubscriptionAsync(
            db, Guid.NewGuid(), "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None));

        // Assert
        Assert.Equal("PERMISSION_DENIED", ex.Code);
        Assert.Empty(workQueue.Items);
    }

    /// <summary>テナント未解決なら 0 件として enqueue しない。</summary>
    [Fact]
    public async Task PublishAsync_WhenTenantUnresolved_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = CreateService(
            transactions,
            waits,
            workQueue,
            tenantContext: NullTenantContextAccessor.Instance);
        await SeedSubscriptionAsync(
            db, Guid.NewGuid(), "WaitNode", "inventory.received", "SKU-1", "statevia.event.subscribe.0", DateTime.UtcNow);

        // Act
        await service.PublishAsync("inventory.received", "SKU-1", CancellationToken.None);

        // Assert
        Assert.Empty(workQueue.Items);
    }

    /// <summary>DisabledTenantQueryFilterOptions でも他テナント購読は明示 TenantId で除外する。</summary>
    [Fact]
    public async Task PublishAsync_WhenQueryFilterDisabled_DoesNotEnqueueOtherTenant()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var workQueue = new CapturingWorkQueue();
        var service = CreateService(transactions, waits, workQueue);
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db, t1Execution, "t1-wait", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(
            db,
            otherExecution,
            "other-wait",
            "shared.topic",
            "k1",
            "statevia.event.subscribe.0",
            now,
            TestTenantIds.OtherTenantId);
        await using (var unfiltered = new CoreDbContext(
            db.Options,
            NullTenantContextAccessor.Instance,
            DisabledTenantQueryFilterOptions.Instance))
        {
            Assert.Equal(2, await unfiltered.Executions.CountAsync());
        }

        // Act
        await service.PublishAsync("shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(t1Execution, workQueue.Items[0].ExecutionId);
        Assert.DoesNotContain(workQueue.Items, item => item.ExecutionId == otherExecution);
    }

    private static EventIngressService CreateService(
        ICoreTransactionExecutor transactions,
        IExecutionWaitRepository waits,
        CapturingWorkQueue workQueue,
        IRuntimePermissionAuthorization? runtimeAuth = null,
        ITenantContextAccessor? tenantContext = null)
    {
        return new EventIngressService(
            transactions,
            waits,
            workQueue,
            new DefaultIdGenerator(),
            runtimeAuth ?? new AllowAllRuntimePermissionAuthorization(),
            tenantContext ?? CreateT1TenantContext());
    }

    private static ITenantContextAccessor CreateT1TenantContext()
    {
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.T1Context);
        return accessor;
    }

    private sealed class DenyingRuntimePermissionAuthorization : IRuntimePermissionAuthorization
    {
        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            _ = permissionKey;
            _ = cancellationToken;
            throw new ForbiddenException("Insufficient permission.", "PERMISSION_DENIED");
        }
    }

    private static async Task SeedSubscriptionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        string nodeId,
        string topic,
        string correlationKey,
        string resumeEventName,
        DateTime createdAt,
        Guid? tenantId = null)
    {
        await SeedExecutionAsync(db, executionId, createdAt, tenantId ?? TestTenantIds.T1TenantId);
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

    private static async Task SeedExecutionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        DateTime createdAt,
        Guid tenantId)
    {
        await using var ctx = new CoreDbContext(db.Options);
        if (await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            return;

        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = tenantId,
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
