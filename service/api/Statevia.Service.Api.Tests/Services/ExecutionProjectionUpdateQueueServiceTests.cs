using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Contracts;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionProjectionUpdateQueueServiceTests
{
    private sealed class FakeExecutionEngine : IExecutionEngine
    {
        private Func<string, Task>? _nodeCompletedHandler;
        private Func<string, string, Task>? _suspendHandler;
        private Func<ForkExpansionEvent, Task>? _forkExpansionHandler;

        public string Start(
            CompiledWorkflowDefinition definition,
            string? executionId = null,
            object? input = null,
            string? initialState = null) =>
            throw new NotSupportedException();

        public void ResumeWaitNode(string executionId, string nodeId, string eventName) =>
            throw new NotSupportedException();

        public void PublishEvent(string executionId, string eventName) =>
            throw new NotSupportedException();

        public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId) =>
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

        public void SetSuspendHandler(Func<string, string, Task>? handler)
        {
            _suspendHandler = handler;
        }

        public void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler)
        {
            _forkExpansionHandler = handler;
        }

        public void CompletePhysicalJoin(
            string executionId,
            string joinStateName,
            IReadOnlyDictionary<string, object?> branchOutputs,
            IReadOnlyList<PhysicalJoinContextFragment>? contextMerges = null)
        {
        }

        public ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId) => null;

        public void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint)
        {
        }

        public bool Unload(string executionId) => false;

        internal Task EmitNodeCompletedAsync(Guid executionId) =>
            _nodeCompletedHandler?.Invoke(executionId.ToString("D")) ?? Task.CompletedTask;

        internal Task EmitSuspendAsync(string executionId, string nodeId) =>
            _suspendHandler?.Invoke(executionId, nodeId) ?? Task.CompletedTask;

        internal Task EmitForkExpansionAsync(ForkExpansionEvent evt) =>
            _forkExpansionHandler?.Invoke(evt) ?? Task.CompletedTask;
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

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) => Task.CompletedTask;
        public Task RecoverExecutionAsync(Guid executionId, CommandRequestContext requestContext, CancellationToken ct) => Task.CompletedTask;
        public Task<ExecutionResponse> ExecuteQueuedStartAsync(Guid executionId, StartExecutionRequest request, CancellationToken ct) => Task.FromResult(new ExecutionResponse());
        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) => Task.CompletedTask;
        public Task<long?> BeginOwnedSessionAsync(Guid executionId, string workerId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult<long?>(1);
        public Task<bool> RenewOwnedSessionLeaseAsync(Guid executionId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult(true);
        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task AbandonLocalOwnedSessionAsync(Guid executionId) => Task.CompletedTask;

        public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;

        internal int PersistCheckpointAndUnloadByEngineIdCallCount { get; private set; }

        public Task PersistCheckpointAndUnloadByEngineIdAsync(string engineExecutionId, string nodeId, CancellationToken ct)
        {
            PersistCheckpointAndUnloadByEngineIdCallCount += 1;
            return Task.CompletedTask;
        }

        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListPageQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>投影更新時にテナント文脈が解決されているか観測する。</summary>
    private sealed class TenantContextObservingExecutionService : IExecutionService
    {
        private readonly ITenantContextAccessor _tenantContextAccessor;

        internal bool WasResolvedDuringUpdate { get; private set; }

        internal Guid? TenantIdDuringUpdate { get; private set; }

        internal TenantContextObservingExecutionService(ITenantContextAccessor tenantContextAccessor) =>
            _tenantContextAccessor = tenantContextAccessor;

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct)
        {
            WasResolvedDuringUpdate = _tenantContextAccessor.IsResolved;
            TenantIdDuringUpdate = _tenantContextAccessor.TenantId;
            return Task.CompletedTask;
        }

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) => Task.CompletedTask;
        public Task RecoverExecutionAsync(Guid executionId, CommandRequestContext requestContext, CancellationToken ct) => Task.CompletedTask;
        public Task<ExecutionResponse> ExecuteQueuedStartAsync(Guid executionId, StartExecutionRequest request, CancellationToken ct) => Task.FromResult(new ExecutionResponse());
        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) => Task.CompletedTask;
        public Task<long?> BeginOwnedSessionAsync(Guid executionId, string workerId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult<long?>(1);
        public Task<bool> RenewOwnedSessionLeaseAsync(Guid executionId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult(true);
        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task AbandonLocalOwnedSessionAsync(Guid executionId) => Task.CompletedTask;

        public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task PersistCheckpointAndUnloadByEngineIdAsync(string engineExecutionId, string nodeId, CancellationToken ct) => Task.CompletedTask;


        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListPageQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
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

        public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) => Task.CompletedTask;
        public Task RecoverExecutionAsync(Guid executionId, CommandRequestContext requestContext, CancellationToken ct) => Task.CompletedTask;
        public Task<ExecutionResponse> ExecuteQueuedStartAsync(Guid executionId, StartExecutionRequest request, CancellationToken ct) => Task.FromResult(new ExecutionResponse());
        public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) => Task.CompletedTask;
        public Task<long?> BeginOwnedSessionAsync(Guid executionId, string workerId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult<long?>(1);
        public Task<bool> RenewOwnedSessionLeaseAsync(Guid executionId, TimeSpan leaseDuration, CancellationToken ct) => Task.FromResult(true);
        public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task AbandonLocalOwnedSessionAsync(Guid executionId) => Task.CompletedTask;

        public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task PersistCheckpointAndUnloadByEngineIdAsync(string engineExecutionId, string nodeId, CancellationToken ct) => Task.CompletedTask;


        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListPageQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// ノード完了通知の投影更新前に execution のテナント文脈が確立されることを確認する。
    /// </summary>
    [Fact]
    public async Task EmitNodeCompletedAsync_EstablishesTenantContext_BeforeProjectionUpdate()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        using var database = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var seed = database.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.T1TenantId, "t1", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.T1TenantId,
                definitionId,
                "wf-projection-queue",
                projectId,
                versionId: versionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = versionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await seed.SaveChangesAsync();
        }

        var executionEngine = new FakeExecutionEngine();
        var tenantContextAccessor = new TenantContextAccessor();
        var executionService = new TenantContextObservingExecutionService(tenantContextAccessor);
        await using var serviceProvider = BuildServiceProvider(
            executionService,
            tenantContextAccessor,
            new PlatformDataAccess(database.Factory, new DefaultIdGenerator()));
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

            // Assert
            Assert.True(executionService.WasResolvedDuringUpdate);
            Assert.Equal(TestTenantIds.T1TenantId, executionService.TenantIdDuringUpdate);
            Assert.False(tenantContextAccessor.IsResolved);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
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

    /// <summary>投影キューが要求する Platform lookup のスタブ。</summary>
    /// <summary>Suspend 通知で PersistCheckpointAndUnload が呼ばれる。</summary>
    [Fact]
    public async Task EmitSuspendAsync_WhenTenantFound_PersistsCheckpointAndUnload()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
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
            await executionEngine.EmitSuspendAsync(executionId.ToString("D"), "wait1");

            // Assert
            Assert.Equal(1, executionService.PersistCheckpointAndUnloadByEngineIdCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>不正な executionId の Suspend は checkpoint をスキップする。</summary>
    [Fact]
    public async Task EmitSuspendAsync_WhenExecutionIdInvalid_SkipsPersist()
    {
        // Arrange
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        await using var serviceProvider = BuildServiceProvider(executionService);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 1,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitSuspendAsync("not-a-guid", "wait1");

            // Assert
            Assert.Equal(0, executionService.PersistCheckpointAndUnloadByEngineIdCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>テナントが無い Suspend は checkpoint をスキップする。</summary>
    [Fact]
    public async Task EmitSuspendAsync_WhenTenantMissing_SkipsPersist()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        var platform = new StubPlatformDataAccess { TenantToReturn = null };
        await using var serviceProvider = BuildServiceProvider(
            executionService,
            new TenantContextAccessor(),
            platform);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 1,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitSuspendAsync(executionId.ToString("D"), "wait1");

            // Assert
            Assert.Equal(0, executionService.PersistCheckpointAndUnloadByEngineIdCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>ForkExpansion 通知で HostHandler が呼ばれる。</summary>
    [Fact]
    public async Task EmitForkExpansionAsync_WhenTenantFound_InvokesHostHandler()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        var forkHandler = new RecordingForkExpansionHostHandler();
        await using var serviceProvider = BuildServiceProvider(executionService, forkHandler: forkHandler);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 1,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitForkExpansionAsync(new ForkExpansionEvent(
                executionId.ToString("D"),
                "fork1",
                "n1",
                [new ForkBranchExpansionPlan("branchA", null)]));

            // Assert
            Assert.Equal(1, forkHandler.HandleCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>不正な executionId の ForkExpansion は HostHandler を呼ばない。</summary>
    [Fact]
    public async Task EmitForkExpansionAsync_WhenExecutionIdInvalid_SkipsHandler()
    {
        // Arrange
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        var forkHandler = new RecordingForkExpansionHostHandler();
        await using var serviceProvider = BuildServiceProvider(executionService, forkHandler: forkHandler);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 1,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitForkExpansionAsync(new ForkExpansionEvent(
                "bad-id",
                "fork1",
                "n1",
                [new ForkBranchExpansionPlan("branchA", null)]));

            // Assert
            Assert.Equal(0, forkHandler.HandleCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>テナント欠落の ForkExpansion は HostHandler を呼ばない。</summary>
    [Fact]
    public async Task EmitForkExpansionAsync_WhenTenantMissing_SkipsHandler()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var executionEngine = new FakeExecutionEngine();
        var executionService = new FakeExecutionService(failuresBeforeSuccess: 0);
        var forkHandler = new RecordingForkExpansionHostHandler();
        var platform = new StubPlatformDataAccess { TenantToReturn = null };
        await using var serviceProvider = BuildServiceProvider(
            executionService,
            new TenantContextAccessor(),
            platform,
            forkHandler);
        var queue = BuildQueueService(executionEngine, serviceProvider, new ExecutionProjectionQueueOptions
        {
            MaxGlobalQueueSize = 10,
            ProjectionFlushDebounceMs = 0,
            MaxRetryAttempts = 1,
            RetryBaseDelayMs = 0,
            RetryMaxDelayMs = 0
        });
        await queue.StartAsync(CancellationToken.None);

        try
        {
            // Act
            await executionEngine.EmitForkExpansionAsync(new ForkExpansionEvent(
                executionId.ToString("D"),
                "fork1",
                "n1",
                [new ForkBranchExpansionPlan("branchA", null)]));

            // Assert
            Assert.Equal(0, forkHandler.HandleCallCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    private sealed class RecordingForkExpansionHostHandler : IForkExpansionHostHandler
    {
        public int HandleCallCount { get; private set; }

        public Task HandleAsync(ForkExpansionEvent evt, CancellationToken ct)
        {
            HandleCallCount += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPlatformDataAccess : IPlatformDataAccess
    {
        public ExecutionTenantLookup? TenantToReturn { get; set; } = new(
            TestTenantIds.T1TenantId,
            "t1",
            TenantLifecycle.Active);

        public Task<ExecutionTenantLookup?> FindExecutionTenantAsync(Guid executionId, CancellationToken cancellationToken) =>
            Task.FromResult(TenantToReturn);

        public Task<TenantRow?> FindTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TenantRow>> ListActiveTenantsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginCredentialLookup?> FindLoginCredentialAsync(string tenantKey, string username, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginCredentialLookup?> FindUserPrincipalAsync(Guid tenantId, Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryUpdateUserPasswordHashAsync(
            Guid tenantId,
            Guid userId,
            string passwordHash,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApiKeyCredentialLookup?> FindApiKeyCredentialAsync(string keyPrefix, string keyHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task TouchApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EnsureDefaultTenantAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PrincipalRow?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static ServiceProvider BuildServiceProvider(
        IExecutionService executionService,
        IForkExpansionHostHandler? forkHandler = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<IPlatformDataAccess, StubPlatformDataAccess>();
        services.AddScoped<IExecutionService>(_ => executionService);
        services.AddScoped<IForkExpansionHostHandler>(_ => forkHandler ?? new RecordingForkExpansionHostHandler());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildServiceProvider(
        IExecutionService executionService,
        ITenantContextAccessor tenantContextAccessor,
        IPlatformDataAccess platformDataAccess,
        IForkExpansionHostHandler? forkHandler = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tenantContextAccessor);
        services.AddSingleton(platformDataAccess);
        services.AddScoped<IExecutionService>(_ => executionService);
        services.AddScoped<IForkExpansionHostHandler>(_ => forkHandler ?? new RecordingForkExpansionHostHandler());
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
