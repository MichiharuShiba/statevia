using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Application.Contracts.Persistence;
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

    private sealed class RecordingWorkQueue : IExecutionWorkQueue
    {
        public int EnqueueExpiredDelayWaitCallCount { get; private set; }
        public int EnqueueManyCallCount { get; private set; }

        public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
        {
            EnqueueManyCallCount++;
            return Task.CompletedTask;
        }

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
            CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<int> EnqueueExpiredDelayWaitResumesAsync(
            DateTime nowUtc,
            int limit,
            CancellationToken ct)
        {
            EnqueueExpiredDelayWaitCallCount++;
            return Task.FromResult(0);
        }
    }
}
