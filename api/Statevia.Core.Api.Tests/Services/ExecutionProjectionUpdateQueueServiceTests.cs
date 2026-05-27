using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Tests.Services;

public sealed class ExecutionProjectionUpdateQueueServiceTests
{
    private sealed class FakeExecutionEngine : IExecutionEngine
    {
        private Func<string, Task>? _nodeCompletedHandler;

        public string Start(CompiledWorkflowDefinition definition, string? executionId = null, object? input = null) =>
            throw new NotSupportedException();

        public void PublishEvent(string executionId, string eventName) =>
            throw new NotSupportedException();

        public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId) =>
            throw new NotSupportedException();

        public void PublishEvent(string eventName) =>
            throw new NotSupportedException();

        public ApplyResult PublishEvent(string eventName, Guid clientEventId) =>
            throw new NotSupportedException();

        public Task CancelAsync(string executionId) =>
            throw new NotSupportedException();

        public Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId) =>
            throw new NotSupportedException();

        public ExecutionSnapshot? GetSnapshot(string executionId) =>
            throw new NotSupportedException();

        public string ExportExecutionGraph(string executionId) =>
            throw new NotSupportedException();

        public void SetNodeCompletedHandler(Func<string, Task>? handler)
        {
            _nodeCompletedHandler = handler;
        }

        internal Task EmitNodeCompletedAsync(Guid executionId) =>
            _nodeCompletedHandler?.Invoke(executionId.ToString("D")) ?? Task.CompletedTask;
    }

    private sealed class FakeExecutionService : IExecutionService
    {
        private int _failuresBeforeSuccess;

        internal int UpdateProjectionCallCount { get; private set; }

        internal FakeExecutionService(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        internal void SetFailuresBeforeSuccess(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct)
        {
            UpdateProjectionCallCount += 1;
            if (_failuresBeforeSuccess > 0)
            {
                _failuresBeforeSuccess -= 1;
                throw new InvalidOperationException("projection update failed");
            }

            return Task.CompletedTask;
        }

        public Task<ExecutionResponse> StartAsync(string tenantId, StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(string tenantId, ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task EnsureExecutionExistsAsync(string tenantId, Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>最初の投影更新だけ完了シグナル待ちでブロックし、グローバルキュー滞留を再現する。</summary>
    private sealed class BlockFirstProjectionUpdateExecutionService : IExecutionService
    {
        private readonly TaskCompletionSource<bool> _firstUpdateMayProceed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _updateProjectionCallCount;

        internal int UpdateProjectionCallCount => _updateProjectionCallCount;

        internal void ReleaseFirstUpdate() => _firstUpdateMayProceed.TrySetResult(true);

        public async Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct)
        {
            var callNumber = Interlocked.Increment(ref _updateProjectionCallCount);
            if (callNumber == 1)
                await _firstUpdateMayProceed.Task.WaitAsync(ct);
        }

        public Task<ExecutionResponse> StartAsync(string tenantId, StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(string tenantId, ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task EnsureExecutionExistsAsync(string tenantId, Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// 連続失敗が上限に達したとき、当該 execution が再投入されず処理が停止することを確認する。
    /// </summary>
    [Fact]
    public async Task EmitNodeCompletedAsync_WhenProjectionUpdateAlwaysFails_StopsRetryAtMaxAttempts()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: int.MaxValue);
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitNodeCompletedAsync(executionId);
            using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(executionId, drainTimeout.Token);

            var callCountAfterDeadLetter = executionService.UpdateProjectionCallCount;
            await executionEngine.EmitNodeCompletedAsync(executionId);
            await Task.Delay(100);

            // Assert
            Assert.Equal(3, callCountAfterDeadLetter);
            Assert.Equal(callCountAfterDeadLetter, executionService.UpdateProjectionCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 失敗後の再試行で、設定したバックオフ時間だけ待機してから再実行されることを確認する。
    /// </summary>
    [Fact]
    public async Task EmitNodeCompletedAsync_WhenProjectionUpdateFailsOnce_AppliesRetryBackoff()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 1);
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 5,
            RetryBaseDelayMs = 120,
            RetryMaxDelayMs = 120
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            var startedAt = DateTime.UtcNow;
            await executionEngine.EmitNodeCompletedAsync(executionId);
            using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await queue.DrainAsync(executionId, drainTimeout.Token);
            var elapsed = DateTime.UtcNow - startedAt;

            // Assert
            Assert.Equal(2, executionService.UpdateProjectionCallCount);
            Assert.True(elapsed >= TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 一度成功した後は連続失敗回数がリセットされ、次回失敗でも再試行できることを確認する。
    /// </summary>
    [Fact]
    public async Task EmitNodeCompletedAsync_WhenUpdateEventuallySucceeds_ResetsFailureCountForNextAttempt()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 1);
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 2,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitNodeCompletedAsync(executionId);
            using var firstDrainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(executionId, firstDrainTimeout.Token);

            executionService.SetFailuresBeforeSuccess(1);
            await executionEngine.EmitNodeCompletedAsync(executionId);
            using var secondDrainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(executionId, secondDrainTimeout.Token);

            // Assert
            Assert.Equal(4, executionService.UpdateProjectionCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 停止要求直前に enqueue された更新が、shutdown ドレインで取りこぼされず 1 回は反映されることを確認する。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenPendingUpdateExists_DrainsBeforeStopping()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 200,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitNodeCompletedAsync(executionId);
            await queue.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, executionService.UpdateProjectionCallCount);
        }
        finally
        {
            // 既に Stop 済みでも二重停止は許容されるため、テスト終了時に安全側で呼ぶ。
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// グローバルキューが満杯のとき、別 execution の投入がブロックし、スロット解放後に完了することを確認する。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenGlobalQueueIsFull_BlocksUntilSlotAvailable()
    {
        // Arrange
        var executionIdFirst = Guid.NewGuid();
        var executionIdSecond = Guid.NewGuid();
        var executionIdThird = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new BlockFirstProjectionUpdateExecutionService();
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 1,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitNodeCompletedAsync(executionIdFirst);
            await Task.Delay(150);
            Assert.Equal(1, executionService.UpdateProjectionCallCount);

            await executionEngine.EmitNodeCompletedAsync(executionIdSecond);
            await Task.Delay(50);

            var thirdEnqueue = Task.Run(() => executionEngine.EmitNodeCompletedAsync(executionIdThird));
            var thirdCompletedWithinTimeout = await Task.WhenAny(thirdEnqueue, Task.Delay(200)) == thirdEnqueue;
            Assert.False(thirdCompletedWithinTimeout);

            executionService.ReleaseFirstUpdate();
            await thirdEnqueue;

            using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await queue.DrainAsync(executionIdFirst, drainTimeout.Token);
            await queue.DrainAsync(executionIdSecond, drainTimeout.Token);
            await queue.DrainAsync(executionIdThird, drainTimeout.Token);

            // Assert
            Assert.True(executionService.UpdateProjectionCallCount >= 3);
        }
        finally
        {
            executionService.ReleaseFirstUpdate();
            await queue.StopAsync(CancellationToken.None);
        }
    }

    private static ServiceProvider BuildServiceProvider(IExecutionService executionService)
    {
        var services = new ServiceCollection();
        services.AddScoped<IExecutionService>(_ => executionService);
        return services.BuildServiceProvider();
    }

    private static ExecutionProjectionUpdateQueueService BuildQueueService(
        IExecutionEngine executionEngine,
        ServiceProvider serviceProvider,
        ExecutionProjectionQueueOptions options)
    {
        return new ExecutionProjectionUpdateQueueService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            executionEngine,
            Options.Create(options),
            NullLogger<ExecutionProjectionUpdateQueueService>.Instance);
    }
}
