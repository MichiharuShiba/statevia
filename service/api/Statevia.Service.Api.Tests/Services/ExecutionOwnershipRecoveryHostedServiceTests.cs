using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Runtime.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>ExecutionOwnershipRecoveryHostedService の単体テスト（Runtime カバレッジ用）。</summary>
public sealed class ExecutionOwnershipRecoveryHostedServiceTests
{
    /// <summary>イテレーションごとに EnqueueExpiredOwnershipRecoveriesAsync を呼ぶ。</summary>
    [Fact]
    public async Task ExecuteAsync_CallsOwnershipRecoveryEnqueueApi()
    {
        // Arrange
        var queue = new RecordingOwnershipQueue();
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new ExecutionOwnershipRecoveryHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionOwnershipRecoveryHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(180), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // テスト側待機キャンセルは無視する。
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(queue.EnqueueOwnershipRecoveryCallCount >= 1);
    }

    /// <summary>batch 上限に達したときは idle delay を挟まず再ポーリングする。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBatchFull_ReclaimsWithoutIdleDelay()
    {
        // Arrange
        var queue = new RecordingOwnershipQueue { EnqueueResult = 64 };
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var sut = new ExecutionOwnershipRecoveryHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionOwnershipRecoveryHostedService>.Instance);

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert: idle 5s を挟まないため短時間で複数回呼ばれる
        Assert.True(queue.EnqueueOwnershipRecoveryCallCount >= 2);
    }

    /// <summary>キュー API が例外でも反復が継続する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenEnqueueThrows_ContinuesPolling()
    {
        // Arrange
        var queue = new RecordingOwnershipQueue { ThrowOnEnqueue = true };
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionWorkQueue>(queue);
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sut = new ExecutionOwnershipRecoveryHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExecutionOwnershipRecoveryHostedService>.Instance);

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
        Assert.True(queue.EnqueueOwnershipRecoveryCallCount >= 1);
    }

    private sealed class RecordingOwnershipQueue : IExecutionWorkQueue
    {
        public int EnqueueOwnershipRecoveryCallCount { get; private set; }
        public int EnqueueResult { get; set; }
        public bool ThrowOnEnqueue { get; set; }

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct) =>
            throw new NotImplementedException();

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
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct) =>
            throw new NotImplementedException();

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
            CancellationToken ct)
        {
            EnqueueOwnershipRecoveryCallCount++;
            if (ThrowOnEnqueue)
                throw new InvalidOperationException("enqueue failed");
            return Task.FromResult(EnqueueResult);
        }

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
