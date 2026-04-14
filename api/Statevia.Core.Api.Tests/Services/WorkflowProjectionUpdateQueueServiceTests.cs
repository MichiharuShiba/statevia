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

public sealed class WorkflowProjectionUpdateQueueServiceTests
{
    private sealed class FakeWorkflowEngine : IWorkflowEngine
    {
        private Func<string, Task>? _nodeCompletedHandler;

        public string Start(CompiledWorkflowDefinition definition, string? workflowId = null, object? workflowInput = null) =>
            throw new NotSupportedException();

        public void PublishEvent(string workflowId, string eventName) =>
            throw new NotSupportedException();

        public ApplyResult PublishEvent(string workflowId, string eventName, Guid clientEventId) =>
            throw new NotSupportedException();

        public void PublishEvent(string eventName) =>
            throw new NotSupportedException();

        public ApplyResult PublishEvent(string eventName, Guid clientEventId) =>
            throw new NotSupportedException();

        public Task CancelAsync(string workflowId) =>
            throw new NotSupportedException();

        public Task<ApplyResult> CancelAsync(string workflowId, Guid clientEventId) =>
            throw new NotSupportedException();

        public WorkflowSnapshot? GetSnapshot(string workflowId) =>
            throw new NotSupportedException();

        public string ExportExecutionGraph(string workflowId) =>
            throw new NotSupportedException();

        public void SetNodeCompletedHandler(Func<string, Task>? handler)
        {
            _nodeCompletedHandler = handler;
        }

        internal Task EmitNodeCompletedAsync(Guid workflowId) =>
            _nodeCompletedHandler?.Invoke(workflowId.ToString("D")) ?? Task.CompletedTask;
    }

    private sealed class FakeWorkflowService : IWorkflowService
    {
        private int _failuresBeforeSuccess;

        internal int UpdateProjectionCallCount { get; private set; }

        internal FakeWorkflowService(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        internal void SetFailuresBeforeSuccess(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct)
        {
            UpdateProjectionCallCount += 1;
            if (_failuresBeforeSuccess > 0)
            {
                _failuresBeforeSuccess -= 1;
                throw new InvalidOperationException("projection update failed");
            }

            return Task.CompletedTask;
        }

        public Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<WorkflowResponse>> ListPagedAsync(string tenantId, int offset, int limit, string? status, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// 連続失敗が上限に達したとき、当該 workflow が再投入されず処理が停止することを確認する。
    /// </summary>
    [Fact]
    public async Task EmitNodeCompletedAsync_WhenProjectionUpdateAlwaysFails_StopsRetryAtMaxAttempts()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflowEngine = new FakeWorkflowEngine();
        var workflowService = new FakeWorkflowService(failuresBeforeSuccess: int.MaxValue);
        await using var serviceProvider = BuildServiceProvider(workflowService);
        var queue = BuildQueueService(workflowEngine, serviceProvider, new WorkflowProjectionQueueOptions
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
            await workflowEngine.EmitNodeCompletedAsync(workflowId);
            using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(workflowId, drainTimeout.Token);

            var callCountAfterDeadLetter = workflowService.UpdateProjectionCallCount;
            await workflowEngine.EmitNodeCompletedAsync(workflowId);
            await Task.Delay(100);

            // Assert
            Assert.Equal(3, callCountAfterDeadLetter);
            Assert.Equal(callCountAfterDeadLetter, workflowService.UpdateProjectionCallCount);
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
        var workflowId = Guid.NewGuid();
        var workflowEngine = new FakeWorkflowEngine();
        var workflowService = new FakeWorkflowService(failuresBeforeSuccess: 1);
        await using var serviceProvider = BuildServiceProvider(workflowService);
        var queue = BuildQueueService(workflowEngine, serviceProvider, new WorkflowProjectionQueueOptions
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
            await workflowEngine.EmitNodeCompletedAsync(workflowId);
            using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await queue.DrainAsync(workflowId, drainTimeout.Token);
            var elapsed = DateTime.UtcNow - startedAt;

            // Assert
            Assert.Equal(2, workflowService.UpdateProjectionCallCount);
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
        var workflowId = Guid.NewGuid();
        var workflowEngine = new FakeWorkflowEngine();
        var workflowService = new FakeWorkflowService(failuresBeforeSuccess: 1);
        await using var serviceProvider = BuildServiceProvider(workflowService);
        var queue = BuildQueueService(workflowEngine, serviceProvider, new WorkflowProjectionQueueOptions
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
            await workflowEngine.EmitNodeCompletedAsync(workflowId);
            using var firstDrainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(workflowId, firstDrainTimeout.Token);

            workflowService.SetFailuresBeforeSuccess(1);
            await workflowEngine.EmitNodeCompletedAsync(workflowId);
            using var secondDrainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await queue.DrainAsync(workflowId, secondDrainTimeout.Token);

            // Assert
            Assert.Equal(4, workflowService.UpdateProjectionCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    private static ServiceProvider BuildServiceProvider(FakeWorkflowService workflowService)
    {
        var services = new ServiceCollection();
        services.AddScoped<IWorkflowService>(_ => workflowService);
        return services.BuildServiceProvider();
    }

    private static WorkflowProjectionUpdateQueueService BuildQueueService(
        IWorkflowEngine workflowEngine,
        ServiceProvider serviceProvider,
        WorkflowProjectionQueueOptions options)
    {
        return new WorkflowProjectionUpdateQueueService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            workflowEngine,
            Options.Create(options),
            NullLogger<WorkflowProjectionUpdateQueueService>.Instance);
    }
}
