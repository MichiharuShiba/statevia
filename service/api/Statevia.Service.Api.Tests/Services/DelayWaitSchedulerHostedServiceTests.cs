using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Runtime.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>DelayWaitScheduler が排他 claim API のみを呼ぶことの単体テスト。</summary>
public sealed class DelayWaitSchedulerHostedServiceTests
{
    /// <summary>イテレーションごとに EnqueueExpiredDelayWaitResumesAsync だけを呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_CallsExclusiveClaimEnqueueApi()
    {
        // Arrange
        var queue = new RecordingWorkQueue();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var sut = new DelayWaitSchedulerHostedService(
            scopeFactory,
            NullLogger<DelayWaitSchedulerHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // テスト側の待機キャンセルは無視する。
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.EnqueueExpiredDelayWaitCallCount >= 1);
        Assert.Equal(0, queue.EnqueueManyCallCount);
    }

    /// <summary>バッチ上限いっぱいなら idle delay なしで再ポーリングする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBatchFull_ContinuesWithoutIdleDelay()
    {
        // Arrange
        var queue = new RecordingWorkQueue { EnqueueResult = 64 };
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new DelayWaitSchedulerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DelayWaitSchedulerHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert — idle 5s を挟まないため短時間で複数回呼べる
        Assert.True(queue.EnqueueExpiredDelayWaitCallCount >= 2);
    }

    /// <summary>enqueue 例外でもポーリングを継続する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenEnqueueThrows_ContinuesPolling()
    {
        // Arrange
        var queue = new RecordingWorkQueue { ThrowOnEnqueue = true };
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new DelayWaitSchedulerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DelayWaitSchedulerHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.EnqueueExpiredDelayWaitCallCount >= 1);
    }

    private sealed class RecordingWorkQueue : IExecutionWorkQueue
    {
        public int EnqueueExpiredDelayWaitCallCount { get; private set; }
        public int EnqueueManyCallCount { get; private set; }
        public int EnqueueResult { get; set; }
        public bool ThrowOnEnqueue { get; set; }

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
        {
            EnqueueManyCallCount++;
            return Task.CompletedTask;
        }

        public Task EnqueueManyAsync(
            ICoreUnitOfWork uow,
            IReadOnlyList<ExecutionWorkItemRow> items,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            int limit,
            IReadOnlyList<string>? kinds,
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool> RenewLeaseAsync(
            Guid workItemId,
            string leaseOwner,
            DateTime utcNow,
            TimeSpan leaseDuration,
            CancellationToken ct) =>
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
            CancellationToken ct)
        {
            EnqueueExpiredDelayWaitCallCount++;
            if (ThrowOnEnqueue)
                throw new InvalidOperationException("enqueue failed");
            return Task.FromResult(EnqueueResult);
        }
    }
}
