using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary><see cref="PublishActionState"/> の ingress 接続と購読再開。</summary>
public sealed class PublishActionStateTests
{
    /// <summary>ingress が呼ばれ、PublishTopic には配送を任せない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTopicPresent_CallsIngressAndDoesNotPublishTopic()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var events = new TrackingEventProvider();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "payment.completed" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(events), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("payment.completed", map["topic"]);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Equal(1, ingress.CallCount);
        Assert.Equal("payment.completed", ingress.LastTopic);
        Assert.Equal(string.Empty, ingress.LastKey);
        Assert.Equal(0, events.PublishTopicCalls);
    }

    /// <summary>key 省略は空文字で ingress に渡す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKeyOmitted_PassesEmptyKey()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "orders.updated" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal(string.Empty, ingress.LastKey);
    }

    /// <summary>key を指定すると trim 前の値を ingress に渡す（正規化は ingress 側）。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenKeyPresent_PassesKeyToIngress()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { topic = "orders.updated", key = "SKU-1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal("SKU-1", ingress.LastKey);
    }

    /// <summary>topic 欠落は ingress を呼ばず失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTopicMissing_ThrowsAndDoesNotCallIngress()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var sut = new PublishActionState(BuildScopeFactory(ingress));
        var input = JsonSerializer.SerializeToElement(new { key = "SKU-1" });

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None));

        // Assert
        Assert.Contains("topic", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, ingress.CallCount);
    }

    /// <summary>subscribe Wait が publish で Resume ワークを受ける。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionMatches_EnqueuesResume()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db, workQueue);
        var executionId = Guid.NewGuid();
        await SeedSubscriptionAsync(
            db,
            new SubscriptionSeed(
                executionId,
                TestTenantIds.T1TenantId,
                "WaitNode",
                "inventory.received",
                "SKU-1",
                "statevia.event.subscribe.0"));
        var input = JsonSerializer.SerializeToElement(new { topic = "inventory.received", key = "SKU-1" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Single(workQueue.Items);
        Assert.Equal(executionId, workQueue.Items[0].ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Resume, workQueue.Items[0].Kind);
    }

    /// <summary>一致 0 件でも Action は成功し enqueue しない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoMatch_SucceedsWithoutEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db, workQueue);
        var input = JsonSerializer.SerializeToElement(new { topic = "missing.topic" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Empty(workQueue.Items);
    }

    /// <summary>同一 topic/key の購読 2 件が両方進む。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTwoSubscriptionsMatch_EnqueuesBoth()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db, workQueue);
        var execution1 = Guid.NewGuid();
        var execution2 = Guid.NewGuid();
        await SeedSubscriptionAsync(
            db,
            new SubscriptionSeed(
                execution1,
                TestTenantIds.T1TenantId,
                "wait-a",
                "inventory.received",
                "SKU-1",
                "statevia.event.subscribe.0"));
        await SeedSubscriptionAsync(
            db,
            new SubscriptionSeed(
                execution2,
                TestTenantIds.T1TenantId,
                "wait-b",
                "inventory.received",
                "SKU-1",
                "statevia.event.subscribe.0"));
        var input = JsonSerializer.SerializeToElement(new { topic = "inventory.received", key = "SKU-1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal(2, workQueue.Items.Count);
        Assert.Contains(workQueue.Items, item => item.ExecutionId == execution1);
        Assert.Contains(workQueue.Items, item => item.ExecutionId == execution2);
    }

    /// <summary>wait.events のみの Wait は購読照合の対象にならない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenSignalWaitOnly_DoesNotEnqueue()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(db, workQueue);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, executionId, TestTenantIds.T1TenantId, now);
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "SignalWait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["ChildCompleted"],
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var input = JsonSerializer.SerializeToElement(new { topic = "child.done", key = "parent-1" });

        // Act
        var result = await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        var map = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.True(Assert.IsType<bool>(map["dispatched"]));
        Assert.Empty(workQueue.Items);
    }

    /// <summary>他テナントの購読は現在テナントの publish では進まない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenOtherTenantSubscription_DoesNotEnqueue()
    {
        // Arrange
        var tenantAccessor = new SettableTenantContextAccessor();
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("statevia-publish-tenant-" + Guid.NewGuid())
            .Options;
        var disabledFactory = new FilterToggleDbContextFactory(options, tenantAccessor, filterEnabled: false);
        var enabledFactory = new FilterToggleDbContextFactory(options, tenantAccessor, filterEnabled: true);
        var now = DateTime.UtcNow;
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        await SeedSubscriptionAsync(
            disabledFactory,
            new SubscriptionSeed(
                t1Execution,
                TestTenantIds.T1TenantId,
                "t1-wait",
                "shared.topic",
                "k1",
                "statevia.event.subscribe.0",
                now));
        await SeedSubscriptionAsync(
            disabledFactory,
            new SubscriptionSeed(
                otherExecution,
                TestTenantIds.OtherTenantId,
                "other-wait",
                "shared.topic",
                "k1",
                "statevia.event.subscribe.0",
                now));
        tenantAccessor.Set(TestTenantIds.T1Context);
        var workQueue = new CapturingWorkQueue();
        var sut = CreateSutWithIngress(enabledFactory, workQueue);
        var input = JsonSerializer.SerializeToElement(new { topic = "shared.topic", key = "k1" });

        // Act
        await sut.ExecuteAsync(MakeContext(new TrackingEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Single(workQueue.Items);
        Assert.Equal(t1Execution, workQueue.Items[0].ExecutionId);
        Assert.DoesNotContain(workQueue.Items, item => item.ExecutionId == otherExecution);
    }

    private static PublishActionState CreateSutWithIngress(InMemoryTestDatabase db, CapturingWorkQueue workQueue)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var ingress = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        return new PublishActionState(BuildScopeFactory(ingress));
    }

    private static PublishActionState CreateSutWithIngress(
        IDbContextFactory<CoreDbContext> dbFactory,
        CapturingWorkQueue workQueue)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(dbFactory);
        var transactions = new TestCoreTransactionExecutor(uowFactory);
        var waits = new ExecutionWaitRepository(dbFactory, new DefaultIdGenerator());
        var ingress = new EventIngressService(transactions, waits, workQueue, new DefaultIdGenerator());
        return new PublishActionState(BuildScopeFactory(ingress));
    }

    private static IServiceScopeFactory BuildScopeFactory(IEventIngressService ingress)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ingress);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static StateContext MakeContext(IEventProvider events) =>
        new()
        {
            Events = events,
            Store = new NoopStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "Publish"
        };

    /// <summary>購読シードの入力。</summary>
    /// <param name="ExecutionId">対象 execution。</param>
    /// <param name="TenantId">execution のテナント。</param>
    /// <param name="NodeId">Wait ノード ID。</param>
    /// <param name="Topic">購読トピック。</param>
    /// <param name="CorrelationKey">購読 key。</param>
    /// <param name="ResumeEventName">Resume に渡す内部イベント名。</param>
    /// <param name="CreatedAt">行の作成時刻。未指定時は UtcNow。</param>
    private sealed record SubscriptionSeed(
        Guid ExecutionId,
        Guid TenantId,
        string NodeId,
        string Topic,
        string CorrelationKey,
        string ResumeEventName,
        DateTime? CreatedAt = null);

    private static async Task SeedSubscriptionAsync(InMemoryTestDatabase db, SubscriptionSeed seed)
    {
        var createdAt = seed.CreatedAt ?? DateTime.UtcNow;
        await SeedExecutionAsync(db, seed.ExecutionId, seed.TenantId, createdAt);
        await using var ctx = new CoreDbContext(db.Options);
        AddWaitAndSubscription(ctx, seed, createdAt);
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedSubscriptionAsync(
        IDbContextFactory<CoreDbContext> factory,
        SubscriptionSeed seed)
    {
        var createdAt = seed.CreatedAt ?? DateTime.UtcNow;
        await using var ctx = factory.CreateDbContext();
        if (!await ctx.Executions.IgnoreQueryFilters().AnyAsync(x => x.ExecutionId == seed.ExecutionId))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = seed.ExecutionId,
                TenantId = seed.TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = createdAt,
                UpdatedAt = createdAt,
                CancelRequested = false,
                RestartLost = false
            });
        }

        AddWaitAndSubscription(ctx, seed, createdAt);
        await ctx.SaveChangesAsync();
    }

    private static void AddWaitAndSubscription(CoreDbContext ctx, SubscriptionSeed seed, DateTime createdAt)
    {
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = seed.ExecutionId,
            NodeId = seed.NodeId,
            WaitKind = ExecutionWaitKind.EventWait,
            AllowedEvents = [seed.ResumeEventName],
            CreatedAt = createdAt
        });
        ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
        {
            SubscriptionId = Guid.NewGuid(),
            ExecutionId = seed.ExecutionId,
            NodeId = seed.NodeId,
            Topic = seed.Topic,
            CorrelationKey = seed.CorrelationKey,
            ResumeEventName = seed.ResumeEventName,
            CreatedAt = createdAt
        });
    }

    private static async Task SeedExecutionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        Guid tenantId,
        DateTime createdAt)
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

    private sealed class FilterToggleDbContextFactory(
        DbContextOptions<CoreDbContext> options,
        ITenantContextAccessor tenantAccessor,
        bool filterEnabled) : IDbContextFactory<CoreDbContext>
    {
        public CoreDbContext CreateDbContext() =>
            new(
                options,
                tenantAccessor,
                filterEnabled
                    ? EnabledTenantQueryFilterOptions.Instance
                    : DisabledTenantQueryFilterOptions.Instance);

        public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class CapturingEventIngressService : IEventIngressService
    {
        public string? LastTopic { get; private set; }
        public string? LastKey { get; private set; }
        public int CallCount { get; private set; }

        public Task PublishAsync(string topic, string key, CancellationToken ct)
        {
            CallCount++;
            LastTopic = topic;
            LastKey = key;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingEventProvider : IEventProvider
    {
        public int PublishTopicCalls { get; private set; }

        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Signal(string signalName) { }

        public void Resume(string nodeId, string eventName) { }

        public void PublishTopic(string topic, object? payloadSummary) => PublishTopicCalls++;
    }

    private sealed class NoopStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
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
