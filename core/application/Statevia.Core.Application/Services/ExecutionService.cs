using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

internal sealed class ExecutionService : IExecutionService
{
    /// <summary>Worker が 1 Load を待つときのポーリング間隔。</summary>
    private static readonly TimeSpan LocalExecutionLoadPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>実行グラフ JSON のプロパティ名（投影・Engine export 共通）。</summary>
    private static class ExecutionGraphJsonProperties
    {
        internal const string Nodes = "nodes";
        internal const string CompletedAt = "completedAt";
        internal const string NodeId = "nodeId";
        internal const string Fact = "fact";
        internal const string Status = "status";
        internal const string StartedAt = "startedAt";
        internal const string NodeType = "nodeType";
    }

    private readonly IExecutionEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly IExecutionRepository _executions;
    private readonly IExecutionWaitRepository _executionWaits;
    private readonly IExecutionCheckpointStore _checkpointStore;
    private readonly IExecutionWorkQueue? _workQueue;
    private readonly ExecutionOwnershipTracker _ownership;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IExecutionMutationAuthorization _mutationAuth;
    private readonly IExecutionSecuritySnapshotFactory _snapshotFactory;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ICommandDedupRepository _dedup;
    private readonly IEventStoreRepository _eventStore;
    private readonly IEventDeliveryDedupRepository _eventDeliveryDedup;
    private readonly IDisplayIdWriteService _displayIdWrites;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IExecutionMutationPersistence _mutationPersistence;
    private readonly ILogger<ExecutionService> _logger;
    private readonly IForkChildExecutionCoordinator? _forkChildCoordinator;
    private readonly ExecutionQueryService _query;
    private readonly ExecutionIdempotencyService _idempotency;
    private readonly ExecutionProjectionOrchestrator _projection;
    private readonly ExecutionLifecycleCommandService _lifecycle;
    private readonly ExecutionWaitEventService _waitEvents;
    private readonly ExecutionCheckpointService _checkpoints;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "DI による明示的コンストラクタ注入。")]
    public ExecutionService(
        IExecutionEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        IExecutionRepository executions,
        IExecutionWaitRepository executionWaits,
        IDefinitionRepository definitions,
        IProjectAuthorizationService projectAuth,
        IRuntimePermissionAuthorization runtimeAuth,
        IExecutionMutationAuthorization mutationAuth,
        IExecutionSecuritySnapshotFactory snapshotFactory,
        ITenantContextAccessor tenantContext,
        ICommandDedupRepository dedup,
        IEventStoreRepository eventStore,
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IDisplayIdWriteService displayIdWrites,
        ICoreTransactionExecutor executor,
        IExecutionMutationPersistence mutationPersistence,
        ILogger<ExecutionService> logger,
        IExecutionCheckpointStore checkpointStore,
        ExecutionQueryService query,
        ExecutionIdempotencyService idempotency,
        ExecutionProjectionOrchestrator projection,
        ExecutionLifecycleCommandService lifecycle,
        ExecutionWaitEventService waitEvents,
        ExecutionCheckpointService checkpoints,
        IExecutionWorkQueue? workQueue = null,
        ExecutionOwnershipTracker? ownershipTracker = null,
        IForkChildExecutionCoordinator? forkChildCoordinator = null)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _executions = executions;
        _executionWaits = executionWaits;
        _checkpointStore = checkpointStore;
        _workQueue = workQueue;
        _ownership = ownershipTracker ?? new ExecutionOwnershipTracker();
        _definitions = definitions;
        _projectAuth = projectAuth;
        _runtimeAuth = runtimeAuth;
        _mutationAuth = mutationAuth;
        _snapshotFactory = snapshotFactory;
        _tenantContext = tenantContext;
        _dedup = dedup;
        _eventStore = eventStore;
        _eventDeliveryDedup = eventDeliveryDedup;
        _displayIdWrites = displayIdWrites;
        _executor = executor;
        _mutationPersistence = mutationPersistence;
        _logger = logger;
        _query = query;
        _idempotency = idempotency;
        _projection = projection;
        _lifecycle = lifecycle;
        _waitEvents = waitEvents;
        _checkpoints = checkpoints;
        _forkChildCoordinator = forkChildCoordinator;
    }

    /// <inheritdoc />
    public Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _lifecycle.StartAsync(request, idempotencyKey, requestContext, ct);

    /// <inheritdoc />
    public Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        _lifecycle.ExecuteQueuedStartAsync(executionId, request, ct);

    public Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListPageQuery query,
        CancellationToken ct) =>
        _query.ListPagedAsync(query, ct);

    public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetExecutionResponseAsync(idOrUuid, ct);

    public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetGraphJsonAsync(idOrUuid, ct);

    public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
        _query.EnsureExecutionExistsAsync(executionId, ct);

    public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
        _query.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, ct);

    /// <inheritdoc />
    public Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _lifecycle.CancelAsync(idOrUuid, idempotencyKey, requestContext, ct);
    /// <inheritdoc />
    public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
        _checkpoints.PersistCheckpointKeepLoadedAsync(engineExecutionId, ct);


    /// <inheritdoc />
    public async Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        var generation = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var seed = new ExecutionCheckpointDocument
                {
                    ExecutionId = executionId,
                    CheckpointJson = "{}",
                    SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                    UpdatedAt = DateTime.UtcNow
                };
                return await _checkpointStore.TryAcquireOwnershipAsync(
                        uow,
                        executionId,
                        workerId,
                        leaseUntil,
                        seed,
                        innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        if (generation is long gen)
            _ownership.Set(executionId, workerId, gen);

        return generation;
    }

    /// <inheritdoc />
    public async Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        // Wait Unload 済みでトラッカーが空なら延長不要（失敗扱いにすると heartbeat が誤キャンセルする）。
        if (!_ownership.TryGet(executionId, out _, out var generation))
            return true;

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        return await _executor.ExecuteReadCommittedAsync(
            (uow, innerCt) => _checkpointStore.TryRenewOwnershipLeaseAsync(
                uow,
                executionId,
                generation,
                leaseUntil,
                innerCt),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct)
    {
        if (!_ownership.TryGet(executionId, out _, out var generation))
            return;

        try
        {
            await _executor.ExecuteReadCommittedAsync(
                (uow, innerCt) => _checkpointStore.TryClearOwnershipAsync(
                    uow,
                    executionId,
                    generation,
                    innerCt),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _ownership.Clear(executionId);
        }
    }

    /// <inheritdoc />
    public Task AbandonLocalOwnedSessionAsync(Guid executionId)
    {
        _ownership.Clear(executionId);
        var engineId = executionId.ToString();
        try
        {
            _engine.Unload(engineId);
        }
#pragma warning disable CA1031 // ローカル放棄はベストエフォート。Unload 失敗で Worker を止めない。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.UnloadDuringAbandon(exception, executionId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct)
    {
        var engineId = executionId.ToString();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = _engine.GetSnapshot(engineId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
            {
                // 終端後に Unload すると投影キューが engine 不在でスキップし、
                // Start 直後の不完全 graph のまま Completed だけ残る。最終投影を先に確定する。
                await _projection.DrainAsync(executionId, ct).ConfigureAwait(false);
                await UpdateProjectionFromEngineAsync(executionId, ct).ConfigureAwait(false);
                _engine.Unload(engineId);
                return;
            }

            var checkpoint = _engine.ExportCheckpoint(engineId);
            // recovery hydrate 後など、PendingWaits があるのに Unload されていない場合がある。
            if (checkpoint is { PendingWaits.Count: > 0 }
                && !HasRunningNonWaitNodes(_engine.ExportExecutionGraph(engineId)))
            {
                var waitNodeId = checkpoint.PendingWaits[0].NodeId;
                await _checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineId, waitNodeId, ct)
                    .ConfigureAwait(false);
                return;
            }

            await Task.Delay(LocalExecutionLoadPollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Wait 以外で未完了の実行中ノードがあるか。</summary>
    private static bool HasRunningNonWaitNodes(string graphJson)
    {
        try
        {
            using var document = JsonDocument.Parse(graphJson);
            if (!document.RootElement.TryGetProperty(ExecutionGraphJsonProperties.Nodes, out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return nodes.EnumerateArray().Any(node =>
            {
                var nodeType = node.TryGetProperty(ExecutionGraphJsonProperties.NodeType, out var typeEl)
                    ? typeEl.GetString()
                    : null;
                if (string.Equals(nodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(nodeType, "EventWait", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (node.TryGetProperty(ExecutionGraphJsonProperties.CompletedAt, out var completedAt)
                    && completedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    return false;
                }

                // startedAt があり未完了なら実行中とみなす。
                return node.TryGetProperty(ExecutionGraphJsonProperties.StartedAt, out var startedAt)
                    && startedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            });
        }
        catch (JsonException)
        {
            return false;
        }
    }
    /// <inheritdoc />
    public Task PublishEventAsync(
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _waitEvents.PublishEventAsync(idOrUuid, eventName, idempotencyKey, requestContext, ct);

    public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetExecutionViewAsync(idOrUuid, ct);

    public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
        _query.GetExecutionViewAtSeqAsync(idOrUuid, atSeq, ct);

    public Task<ExecutionEventsResponseDto> ListEventsAsync(
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct) =>
        _query.ListEventsAsync(idOrUuid, afterSeq, limit, ct);
    /// <inheritdoc />
    public Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _waitEvents.ResumeNodeAsync(
            idOrUuid,
            nodeId,
            resumeKey,
            idempotencyKey,
            requestContext,
            HandleChildCompletedResumeAsync,
            _checkpoints.ShouldDiscardRuntimeCheckpointAsync,
            _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync,
            ct);

    /// <summary>
    /// 物理子終端の予約 Resume を受け、親の Join を再評価する（D2-B / D3）。
    /// </summary>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="execution">親の投影行。</param>
    /// <param name="forkNodeId">Resume payload の nodeId（Fork 到達インスタンス）。</param>
    /// <param name="ct">キャンセル。</param>
    private async Task HandleChildCompletedResumeAsync(
        Guid parentExecutionId,
        ExecutionRow execution,
        string forkNodeId,
        CancellationToken ct)
    {
        if (_forkChildCoordinator is null)
            return;

        if (ExecutionLifecycleCommandService.IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        var evaluation = await _forkChildCoordinator
            .EvaluateJoinAsync(parentExecutionId, forkNodeId, ct)
            .ConfigureAwait(false);

        switch (evaluation.Kind)
        {
            case ForkJoinEvaluationKind.Waiting:
                return;

            case ForkJoinEvaluationKind.Failed:
                await FailParentFromJoinAsync(
                        parentExecutionId,
                        forkNodeId,
                        evaluation.FailureStatus ?? ExecutionProjectionStatuses.Failed,
                        ct)
                    .ConfigureAwait(false);
                return;

            case ForkJoinEvaluationKind.Satisfied:
                // 既に当該 Fork 訪問の Join が投影上 SUCCEEDED なら再実行しない（child.completed 再送の冪等）。
                var snapshotRow = await _executor.ExecuteReadOnlyAsync(
                        (uow, innerCt) => _executions.GetSnapshotByExecutionIdAsync(
                            uow,
                            parentExecutionId,
                            innerCt),
                        ct)
                    .ConfigureAwait(false);
                if (snapshotRow is not null
                    && IsPhysicalJoinAlreadyCompleted(
                        snapshotRow.GraphJson,
                        forkNodeId,
                        evaluation.JoinState))
                {
                    _logger.ForkJoinAlreadyCompleted(parentExecutionId, forkNodeId, evaluation.JoinState);
                    return;
                }

                await CompleteParentPhysicalJoinAsync(
                        parentExecutionId,
                        execution,
                        forkNodeId,
                        evaluation,
                        ct)
                    .ConfigureAwait(false);
                return;

            default:
                throw new InvalidOperationException($"Unknown fork join evaluation kind '{evaluation.Kind}'.");
        }
    }

    /// <summary>
    /// Unload 後でも、Join が進んで Wait / 終端断面が checkpoint に残っているか。
    /// </summary>
    private async Task<bool> HasPhysicalJoinProgressAfterUnloadAsync(
        Guid parentExecutionId,
        CancellationToken ct)
    {
        var document = await _executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => _checkpointStore.GetByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        if (document is null
            || !ExecutionLifecycleCommandService.TryParseRuntimeCheckpoint(document.CheckpointJson, out var checkpoint, out _))
        {
            return false;
        }

        if (checkpoint.IsCompleted || checkpoint.IsCancelled || checkpoint.IsFailed)
            return true;

        if (checkpoint.PendingWaits.Count > 0)
            return true;

        return checkpoint.ActiveStates.Count > 0;
    }

    /// <summary>
    /// 親グラフ上で、当該 Fork 訪問に対応する Join が既に SUCCEEDED か。
    /// </summary>
    /// <remarks>単体テストから到達するため internal。</remarks>
    internal static bool IsPhysicalJoinAlreadyCompleted(
        string parentGraphJson,
        string forkNodeId,
        string joinState)
    {
        var joinNodeId = PhysicalForkGraphComposer.ResolveJoinNodeId(
            parentGraphJson,
            forkNodeId,
            joinState);
        if (string.IsNullOrWhiteSpace(joinNodeId))
            return false;

        try
        {
            using var document = JsonDocument.Parse(parentGraphJson);
            if (!document.RootElement.TryGetProperty(ExecutionGraphJsonProperties.Nodes, out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            // joinNodeId は Fork 訪問ごとに一意。一致ノードが無ければ未完了扱い。
            var node = nodes.EnumerateArray().FirstOrDefault(candidate =>
                candidate.TryGetProperty(ExecutionGraphJsonProperties.NodeId, out var idEl)
                && string.Equals(idEl.GetString(), joinNodeId, StringComparison.Ordinal));
            if (node.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // 投影 JSON は UI 用 status ではなく fact / completedAt を持つ。
            // 当該 Fork 訪問に紐づく Join が既に Joined なら冪等スキップ（循環の別訪問は別 Join nodeId）。
            var fact = node.TryGetProperty(ExecutionGraphJsonProperties.Fact, out var factEl)
                ? factEl.GetString()
                : null;
            if (string.Equals(fact, "Joined", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fact, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (node.TryGetProperty(ExecutionGraphJsonProperties.CompletedAt, out var completedAtEl)
                && completedAtEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(completedAtEl.GetString()))
            {
                return true;
            }

            var status = node.TryGetProperty(ExecutionGraphJsonProperties.Status, out var statusEl)
                ? statusEl.GetString()
                : null;
            return string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Join 失敗／キャンセルを親投影へ伝播する（残子 Cancel はタスク 6）。</summary>
    private async Task FailParentFromJoinAsync(
        Guid parentExecutionId,
        string forkNodeId,
        string failureStatus,
        CancellationToken ct)
    {
        _logger.ForkJoinFailedPropagated(parentExecutionId, forkNodeId, failureStatus);

        var snapshot = await _executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => _executions.GetSnapshotByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        var graphJson = snapshot?.GraphJson ?? """{"nodes":[],"edges":[]}""";
        var cancelRequested = string.Equals(
            failureStatus,
            ExecutionProjectionStatuses.Cancelled,
            StringComparison.Ordinal);

        await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            failureStatus,
                            cancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);

                    await _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            parentExecutionId.ToString(),
                            parentExecutionId,
                            innerCt)
                        .ConfigureAwait(false);
                },
                ct)
            .ConfigureAwait(false);

        _engine.Unload(parentExecutionId.ToString());
    }

    /// <summary>Join 充足時に親 Engine で物理 Join を完了し投影を更新する。</summary>
    private async Task CompleteParentPhysicalJoinAsync(
        Guid parentExecutionId,
        ExecutionRow execution,
        string forkNodeId,
        ForkJoinEvaluation evaluation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evaluation.CandidateInputs);
        _logger.ForkJoinSatisfied(parentExecutionId, forkNodeId, evaluation.JoinState);

        await _lifecycle.EnsureEngineRuntimeLoadedForMutationAsync(parentExecutionId, execution, ct).ConfigureAwait(false);

        var engineId = parentExecutionId.ToString();
        IReadOnlyList<PhysicalJoinContextFragment>? fragments = null;
        if (evaluation.ContextMerges is { Count: > 0 })
        {
            fragments = evaluation.ContextMerges
                .Select(m => new PhysicalJoinContextFragment(m.States, m.Vars))
                .ToList();
        }

        _engine.CompletePhysicalJoin(
            engineId,
            evaluation.JoinState,
            evaluation.CandidateInputs,
            fragments);
        // Join は Schedule 非同期のため、ActiveStates==0 ですぐ戻ると decide Wait 前の断面を永続してしまう。
        await WaitForPhysicalJoinSettlementAsync(
            engineId,
            forkNodeId,
            evaluation.JoinState,
            ct).ConfigureAwait(false);

        // Join 直後に Wait へ進むと SuspendHandler が PersistCheckpointAndUnload する。
        // Unload 後は GetSnapshot が null になり得る。checkpoint に Wait / 終端が進んでいれば冪等完了。
        // Join が startedJoins 等で無動作のまま Unload された場合は再試行する。
        var (status, cancelRequested, graphJson) = _projection.BuildProjectionFromEngineForQueue(parentExecutionId);
        if (status is null || graphJson is null)
        {
            if (!await HasPhysicalJoinProgressAfterUnloadAsync(parentExecutionId, ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"Physical join for execution '{parentExecutionId:D}' unloaded before Join advanced; will retry.");
            }

            // SuspendHandler が checkpoint だけ進めて snapshot が遅れている場合の補正。
            await TrySyncGraphSnapshotFromRuntimeCheckpointAsync(parentExecutionId, execution, ct)
                .ConfigureAwait(false);

            _logger.ForkJoinProjectionSkippedAfterUnload(
                parentExecutionId,
                forkNodeId,
                evaluation.JoinState);
            return;
        }

        await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    // Settlement 待機後〜tx 開始前に Wait Persist が進んでいることがあるので直前に再取得。
                    var (freshStatus, freshCancel, freshGraph) = _projection.BuildProjectionFromEngineForQueue(parentExecutionId);
                    var writeStatus = freshStatus ?? status;
                    var writeCancel = freshCancel ?? cancelRequested;
                    var writeGraph = freshGraph ?? graphJson;

                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            writeStatus,
                            writeCancel,
                            writeGraph,
                            innerCt)
                        .ConfigureAwait(false);
                    await _projection.SyncOperationalProjectionAsync(
                            uow,
                            parentExecutionId,
                            execution.TenantId,
                            writeStatus,
                            writeGraph,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    if (await _checkpoints.ShouldDiscardRuntimeCheckpointAsync(
                                parentExecutionId,
                                writeStatus,
                                writeGraph,
                                innerCt)
                            .ConfigureAwait(false))
                    {
                        await _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                                uow,
                                engineId,
                                parentExecutionId,
                                innerCt)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await _lifecycle.UpsertRuntimeCheckpointAsync(uow, engineId, parentExecutionId, innerCt)
                            .ConfigureAwait(false);
                    }
                },
                ct)
            .ConfigureAwait(false);

        await _projection.EnqueueAsync(parentExecutionId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecoverExecutionAsync(
        Guid executionId,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        if (ExecutionLifecycleCommandService.IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        // Worker が BeginOwnedSession 済み前提。hydrate 後、完了フロンティア／PendingWaits から継続する。
        await _lifecycle.EnsureEngineRuntimeLoadedForMutationAsync(executionId, execution, ct).ConfigureAwait(false);

        var engineId = executionId.ToString();
        // ImportCheckpoint が起動した継続（Wait 復元または完了フロンティア遷移）が落ち着くのを待つ。
        await _waitEvents.WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        var (status, cancelRequested, graphJson) = _projection.BuildProjectionFromEngine(executionId);
        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, executionId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
                await _projection.SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        tenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);

                if (await _checkpoints.ShouldDiscardRuntimeCheckpointAsync(executionId, status, graphJson, innerCt)
                        .ConfigureAwait(false))
                {
                    await _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineId,
                            executionId,
                            innerCt)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _lifecycle.UpsertRuntimeCheckpointAsync(uow, engineId, executionId, innerCt)
                        .ConfigureAwait(false);
                }
            },
            ct).ConfigureAwait(false);

        // Wait 復元のみで継続が無い場合、ここで Unload しないと Worker Await が解放されない。
        var restored = _engine.ExportCheckpoint(engineId);
        if (restored is { PendingWaits.Count: > 0 }
            && !HasRunningNonWaitNodes(_engine.ExportExecutionGraph(engineId)))
        {
            await _checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineId, restored.PendingWaits[0].NodeId, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 物理 Join 完了後、Join ノード完了・decide Wait 登録・Unload・終端のいずれかまで待つ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IExecutionEngine.CompletePhysicalJoin"/> は Join を非同期 Schedule するだけなので、
    /// 直後は <c>ActiveStates</c> が空のままである。ここで
    /// <see cref="ExecutionWaitEventService.WaitForEngineQuiescedAfterWaitAsync"/> を使うと Join 前断面を投影してしまう。
    /// </para>
    /// <para>
    /// Join 直後に長い Action が続く場合でも、Join ノード完了で抜けて投影する。
    /// durable Wait 登録・Unload・終端も同様に出口とする。
    /// </para>
    /// </remarks>
    /// <param name="engineExecutionId">Engine 辞書キー。</param>
    /// <param name="forkNodeId">充足した物理 Fork の graph nodeId。</param>
    /// <param name="joinState">対応する Join 状態名。</param>
    /// <param name="ct">キャンセル。</param>
    private async Task WaitForPhysicalJoinSettlementAsync(
        string engineExecutionId,
        string forkNodeId,
        string joinState,
        CancellationToken ct)
    {
        const int maxAttempts = 200;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = _engine.GetSnapshot(engineExecutionId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
                return;

            var graphJson = _engine.ExportExecutionGraph(engineExecutionId);
            if (IsPhysicalJoinAlreadyCompleted(graphJson, forkNodeId, joinState))
                return;

            var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
            if (checkpoint is { PendingWaits.Count: > 0 })
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        _logger.WaitResumeQuiesceTimedOut(engineExecutionId);
    }

    /// <summary>
    /// Unload 後に runtime checkpoint が Wait 断面まで進んでいるとき、graph snapshot を追いつかせる。
    /// </summary>
    private async Task TrySyncGraphSnapshotFromRuntimeCheckpointAsync(
        Guid executionId,
        ExecutionRow execution,
        CancellationToken ct)
    {
        await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var runtime = await _checkpointStore
                        .GetByExecutionIdAsync(uow, executionId, innerCt)
                        .ConfigureAwait(false);
                    if (runtime is null
                        || !ExecutionLifecycleCommandService.TryParseRuntimeCheckpoint(runtime.CheckpointJson, out var checkpoint, out _)
                        || checkpoint.PendingWaits.Count == 0)
                    {
                        return;
                    }

                    var graphJson = _engine.ExportExecutionGraph(executionId.ToString());
                    if (graphJson is "{}" || string.IsNullOrWhiteSpace(graphJson))
                    {
                        // Engine Unload 済みなら checkpoint 内グラフを投影 JSON へ写す。
                        graphJson = JsonSerializer.Serialize(
                            new
                            {
                                nodes = checkpoint.Graph.Nodes,
                                edges = checkpoint.Graph.Edges
                            },
                            JsonSerializerProfiles.CamelCase);
                    }

                    var existingSnapshot = await _executions
                        .GetSnapshotByExecutionIdAsync(uow, executionId, innerCt)
                        .ConfigureAwait(false);
                    if (existingSnapshot?.GraphJson is not null
                        && !IsGraphSnapshotBehindCheckpoint(existingSnapshot.GraphJson, checkpoint))
                    {
                        return;
                    }

                    var status = execution.Status;
                    if (string.IsNullOrWhiteSpace(status))
                        status = "Running";

                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            executionId,
                            status,
                            execution.CancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);
                    await _projection.SyncOperationalProjectionAsync(
                            uow,
                            executionId,
                            execution.TenantId,
                            status,
                            graphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    _logger.SyncedGraphSnapshotFromRuntimeCheckpoint(
                        executionId,
                        checkpoint.PendingWaits.Count,
                        checkpoint.Graph.Nodes.Count);
                },
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>永続 graph snapshot が runtime checkpoint より遅れているか。</summary>
    private static bool IsGraphSnapshotBehindCheckpoint(
        string snapshotGraphJson,
        ExecutionRuntimeCheckpoint checkpoint)
    {
        if (!TryCountGraphNodes(snapshotGraphJson, out var snapshotNodeCount))
            return true;

        if (snapshotNodeCount < checkpoint.Graph.Nodes.Count)
            return true;

        return checkpoint.PendingWaits.Count > 0
            && !ExecutionOperationalProjectionSync.HasDurableWaits(snapshotGraphJson);
    }

    /// <summary>グラフ JSON のノード数を数える。</summary>
    private static bool TryCountGraphNodes(string graphJson, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(graphJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(graphJson);
            if (!document.RootElement.TryGetProperty(ExecutionGraphJsonProperties.Nodes, out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            count = nodes.GetArrayLength();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    private Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return _mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }
    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
        _checkpoints.PersistCheckpointAndUnloadAsync(executionId, nodeId, ct);
    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct) =>
        _checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineExecutionId, nodeId, ct);

    public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
        _projection.UpdateProjectionFromEngineAsync(
            executionId,
            _checkpoints.ShouldDiscardRuntimeCheckpointAsync,
            _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync,
            _lifecycle.UpsertRuntimeCheckpointAsync,
            ct);

}
