using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

internal sealed class ExecutionService : IExecutionService
{
    /// <summary>Worker が 1 Load を待つときのポーリング間隔。</summary>
    private static readonly TimeSpan LocalExecutionLoadPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// 同一実行の <see cref="PersistCheckpointAndUnloadCoreAsync"/> を直列化し、
    /// 古い Fork Unload が新しい Wait 断面を上書きしないようにする。
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PersistUnloadGates = new();

    /// <summary>HTTP 204 No Content。</summary>
    private const int HttpStatus204NoContent = 204;

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
    public async Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineExecutionId);
        if (!Guid.TryParse(engineExecutionId, out var executionId))
        {
            _logger.SkipKeepLoadedCheckpointInvalidExecutionId(engineExecutionId);
            return;
        }

        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
            return;

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;

        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (_ownership.TryGet(executionId, out _, out var generation))
                {
                    var ok = await _checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                            uow,
                            new ExecutionCheckpointRuntimeUpsert(
                                executionId,
                                generation,
                                json,
                                checkpoint.SchemaVersion,
                                updatedAt),
                            innerCt)
                        .ConfigureAwait(false);
                    if (!ok)
                    {
                        _logger.KeepLoadedCheckpointRejectedByFencing(executionId);
                    }

                    return;
                }

                await _checkpointStore.UpsertAsync(
                        uow,
                        new ExecutionCheckpointDocument
                        {
                            ExecutionId = executionId,
                            CheckpointJson = json,
                            SchemaVersion = checkpoint.SchemaVersion,
                            UpdatedAt = updatedAt
                        },
                        innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

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
                await PersistCheckpointAndUnloadByEngineIdAsync(engineId, waitNodeId, ct)
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
            ShouldDiscardRuntimeCheckpointAsync,
            DiscardOrRefreshRuntimeCheckpointAsync,
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

                    await DiscardOrRefreshRuntimeCheckpointAsync(
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

                    if (await ShouldDiscardRuntimeCheckpointAsync(
                                parentExecutionId,
                                writeStatus,
                                writeGraph,
                                innerCt)
                            .ConfigureAwait(false))
                    {
                        await DiscardOrRefreshRuntimeCheckpointAsync(
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

                if (await ShouldDiscardRuntimeCheckpointAsync(executionId, status, graphJson, innerCt)
                        .ConfigureAwait(false))
                {
                    await DiscardOrRefreshRuntimeCheckpointAsync(
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
            await PersistCheckpointAndUnloadByEngineIdAsync(engineId, restored.PendingWaits[0].NodeId, ct)
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

    /// <summary>
    /// 寿命表に従い checkpoint <b>内容</b>の破棄候補か。
    /// </summary>
    /// <remarks>
    /// 保持理由は <see cref="RuntimeCheckpointRetainReasons"/> のみ
    ///（PendingWaits / PendingPhysicalJoin）。
    /// Worker 所有 lease は同列にせず、破棄候補でも
    /// <see cref="DiscardOrRefreshRuntimeCheckpointAsync"/> が Delete を抑止する。
    /// </remarks>
    private async Task<bool> ShouldDiscardRuntimeCheckpointAsync(
        Guid executionId,
        string status,
        string graphJson,
        CancellationToken ct)
    {
        var retainReasons = await EvaluateRuntimeCheckpointRetainReasonsAsync(
                executionId,
                graphJson,
                ct)
            .ConfigureAwait(false);
        return RuntimeCheckpointLifetimePolicy.IsContentDiscardCandidate(status, retainReasons);
    }

    /// <summary>
    /// 寿命表の内容保持理由を評価する（OwnedLease は含めない）。
    /// </summary>
    private async Task<RuntimeCheckpointRetainReasons> EvaluateRuntimeCheckpointRetainReasonsAsync(
        Guid executionId,
        string graphJson,
        CancellationToken ct)
    {
        var reasons = RuntimeCheckpointRetainReasons.None;

        if (ExecutionOperationalProjectionSync.HasDurableWaits(graphJson))
            reasons |= RuntimeCheckpointRetainReasons.PendingWaits;

        if (_forkChildCoordinator is not null
            && await _forkChildCoordinator
                .HasPendingPhysicalJoinBranchesAsync(executionId, ct)
                .ConfigureAwait(false))
        {
            reasons |= RuntimeCheckpointRetainReasons.PendingPhysicalJoin;
        }

        return reasons;
    }

    /// <summary>
    /// 内容破棄候補の checkpoint を削除するか、Worker 所有中なら runtime JSON のみ更新する。
    /// </summary>
    /// <remarks>
    /// 所有（OwnedLease）は寿命表の保持理由ではない。Worker 都合で行を残し refresh する別層。
    /// </remarks>
    /// <returns>
    /// 投影同期後に Engine を Unload してよいとき <see langword="true"/>。
    /// 所有中の refresh や削除のみの場合は <see langword="false"/>。
    /// </returns>
    private async Task<bool> DiscardOrRefreshRuntimeCheckpointAsync(
        ICoreUnitOfWork uow,
        string engineExecutionId,
        Guid executionId,
        CancellationToken ct)
    {
        if (_ownership.TryGet(executionId, out _, out _))
        {
            // Worker 所有中。寿命表上は破棄候補でも lease 行は残し runtime だけ更新する。
            _ = await _lifecycle.UpsertRuntimeCheckpointAsync(uow, engineExecutionId, executionId, ct)
                .ConfigureAwait(false);
            return false;
        }

        await _checkpointStore.DeleteAsync(uow, executionId, ct).ConfigureAwait(false);
        return false;
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

    /// <summary>
    /// Wait 登録直後: チェックポイントを保存して Unload する。
    /// </summary>
    /// <remarks>
    /// Engine 辞書キーは <see cref="Guid.ToString()"/>（Start 時と同じ形式）を使う。
    /// Export できない場合は Warning を出し、Unload しない（インメモリ再開を維持する）。
    /// </remarks>
    public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
        PersistCheckpointAndUnloadCoreAsync(executionId.ToString(), executionId, nodeId, ct);

    /// <summary>
    /// Engine 実行 ID 文字列を正としてチェックポイントを保存し Unload する。
    /// </summary>
    /// <param name="engineExecutionId">Engine に渡した実行 ID（辞書キー）。</param>
    /// <param name="nodeId">Wait ノード ID（ログ用）。</param>
    /// <param name="ct">キャンセル。</param>
    public Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineExecutionId);
        if (!Guid.TryParse(engineExecutionId, out var executionId))
        {
            _logger.SkipCheckpointPersistInvalidExecutionId(engineExecutionId);
            return Task.CompletedTask;
        }

        return PersistCheckpointAndUnloadCoreAsync(engineExecutionId, executionId, nodeId, ct);
    }

    /// <summary>チェックポイント upsert ののち Engine から Unload する共通実装。</summary>
    private async Task PersistCheckpointAndUnloadCoreAsync(
        string engineExecutionId,
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var gate = PersistUnloadGates.GetOrAdd(executionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PersistCheckpointAndUnloadUnderGateAsync(engineExecutionId, executionId, nodeId, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>ゲート取得後の Persist + Unload 本体。</summary>
    private async Task PersistCheckpointAndUnloadUnderGateAsync(
        string engineExecutionId,
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
        {
            _logger.ExportCheckpointNullSkipUnload(engineExecutionId, nodeId);
            return;
        }

        // ForkExpansion は fire-and-forget のため ExpandFork 完了が Join→decide より遅れることがある。
        // その遅延 Persist が空断面で Unload すると Wait 登録前／登録直後の decide を潰す（初回 Wait 到達不能）。
        if (IsForkExpansionUnloadObsolete(checkpoint, nodeId))
        {
            _logger.SkipForkExpansionUnloadAlreadyProgressed(
                executionId,
                nodeId,
                checkpoint.ActiveStates.Count,
                checkpoint.PendingWaits.Count);
            return;
        }

        // Unload 前に投影へ反映する（Unload 後は GetSnapshot が null になり投影キューが空振りする）。
        // snapshot 欠落時は Unknown/`{}` を書かず checkpoint のみ残す（マルチ Worker 競合で UI を壊さない）。
        var snapshot = _engine.GetSnapshot(engineExecutionId);
        string? status = null;
        bool? cancelRequested = null;
        string? graphJson = null;
        if (snapshot is not null)
        {
            status = ExecutionProjectionOrchestrator.MapStatus(snapshot);
            cancelRequested = snapshot.IsCancelled;
            graphJson = _engine.ExportExecutionGraph(engineExecutionId);
        }

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;
        var (fenceLost, skippedStalePersist) = await PersistCheckpointDocumentUnderTransactionAsync(
                new CheckpointDocumentPersistRequest(
                    executionId,
                    nodeId,
                    checkpoint,
                    json,
                    updatedAt,
                    status,
                    cancelRequested,
                    graphJson),
                ct)
            .ConfigureAwait(false);

        if (skippedStalePersist)
            return;

        _ownership.Clear(executionId);
        _engine.Unload(engineExecutionId);

        if (fenceLost)
        {
            _logger.UnloadAfterWaitFencingLost(nodeId, executionId);
            return;
        }

        _logger.CheckpointUnloadedAfterWait(executionId, nodeId);
    }

    /// <summary>Persist ゲート内の checkpoint 文書 Upsert 入力。</summary>
    private readonly record struct CheckpointDocumentPersistRequest(
        Guid ExecutionId,
        string NodeId,
        ExecutionRuntimeCheckpoint Checkpoint,
        string CheckpointJson,
        DateTime UpdatedAt,
        string? Status,
        bool? CancelRequested,
        string? GraphJson);

    /// <summary>
    /// Persist ゲート内トランザクション: 投影更新・checkpoint Upsert・所有クリア。
    /// </summary>
    /// <returns>fencing 喪失と stale skip の有無。</returns>
    private async Task<(bool FenceLost, bool SkippedStalePersist)> PersistCheckpointDocumentUnderTransactionAsync(
        CheckpointDocumentPersistRequest request,
        CancellationToken ct)
    {
        var skippedStalePersist = false;
        var fenceLost = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var existingCheckpoint = await _checkpointStore
                    .GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (existingCheckpoint is not null
                    && ExecutionLifecycleCommandService.TryParseRuntimeCheckpoint(existingCheckpoint.CheckpointJson, out var stored, out _)
                    && IsRuntimeCheckpointLessAdvanced(request.Checkpoint, stored))
                {
                    skippedStalePersist = true;
                    _logger.SkipStaleCheckpointPersist(
                        request.ExecutionId,
                        request.NodeId,
                        stored.PendingWaits.Count,
                        request.Checkpoint.PendingWaits.Count,
                        stored.Graph.Nodes.Count,
                        request.Checkpoint.Graph.Nodes.Count);
                    return false;
                }

                var execution = await _executions.GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (execution is not null && request.Status is not null && request.GraphJson is not null)
                {
                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            request.ExecutionId,
                            request.Status,
                            request.CancelRequested,
                            request.GraphJson,
                            innerCt)
                        .ConfigureAwait(false);
                    await _projection.SyncOperationalProjectionAsync(
                            uow,
                            request.ExecutionId,
                            execution.TenantId,
                            request.Status,
                            request.GraphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);
                }

                if (_ownership.TryGet(request.ExecutionId, out _, out var generation))
                {
                    var upserted = await _checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                            uow,
                            new ExecutionCheckpointRuntimeUpsert(
                                request.ExecutionId,
                                generation,
                                request.CheckpointJson,
                                request.Checkpoint.SchemaVersion,
                                request.UpdatedAt),
                            innerCt)
                        .ConfigureAwait(false);
                    if (!upserted)
                        return true;

                    await _checkpointStore.TryClearOwnershipAsync(uow, request.ExecutionId, generation, innerCt)
                        .ConfigureAwait(false);
                    return false;
                }

                await _checkpointStore.UpsertAsync(
                    uow,
                    new ExecutionCheckpointDocument
                    {
                        ExecutionId = request.ExecutionId,
                        CheckpointJson = request.CheckpointJson,
                        SchemaVersion = request.Checkpoint.SchemaVersion,
                        UpdatedAt = request.UpdatedAt
                    },
                    innerCt).ConfigureAwait(false);

                var existing = await _checkpointStore.GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (existing?.OwnerWorkerId is not null)
                {
                    await _checkpointStore.TryClearOwnershipAsync(
                            uow,
                            request.ExecutionId,
                            existing.OwnerGeneration,
                            innerCt)
                        .ConfigureAwait(false);
                }

                return false;
            },
            ct).ConfigureAwait(false);

        return (fenceLost, skippedStalePersist);
    }

    /// <summary>
    /// 永続済み断面より incoming が遅れているか（古い Fork Unload の上書き防止）。
    /// </summary>
    /// <remarks>単体テストから到達するため internal。</remarks>
    internal static bool IsRuntimeCheckpointLessAdvanced(
        ExecutionRuntimeCheckpoint incoming,
        ExecutionRuntimeCheckpoint stored)
    {
        if (incoming.IsCompleted || incoming.IsCancelled || incoming.IsFailed)
            return false;

        if (incoming.Graph.Nodes.Count < stored.Graph.Nodes.Count)
            return true;

        // stored に durable Wait があり、incoming が Fork 待ち相当（active/wait 空）なら古い。
        return stored.PendingWaits.Count > 0
            && incoming.PendingWaits.Count == 0
            && incoming.ActiveStates.Count == 0;
    }

    /// <summary>
    /// Fork 展開後の Persist が、当該 Fork より先の Join / Wait 進行後に遅延したか。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosted では <c>NotifyForkExpansion</c> が非同期のため、子完了→Join→decide が
    /// <c>ExpandFork</c> 完了より先に進みうる。その場合の Unload は decide Wait を失わせる。
    /// </para>
    /// <para>
    /// <paramref name="nodeId"/> が Fork ノードでない呼び出し（Wait Suspend）は常に false。
    /// </para>
    /// <para>単体テストから到達するため internal。</para>
    /// </remarks>
    /// <param name="checkpoint">Export 直後の断面。</param>
    /// <param name="nodeId">Persist 要求元のノード ID（Fork 展開時は Fork ノード）。</param>
    /// <returns>Unload をスキップすべきなら true。</returns>
    internal static bool IsForkExpansionUnloadObsolete(
        ExecutionRuntimeCheckpoint checkpoint,
        string nodeId)
    {
        var forkNode = checkpoint.Graph.Nodes
            .FirstOrDefault(n =>
                string.Equals(n.NodeId, nodeId, StringComparison.Ordinal)
                && string.Equals(n.NodeType, "Fork", StringComparison.OrdinalIgnoreCase));
        if (forkNode is null)
            return false;

        // Join 後の decide 実行中、または durable Wait 登録済みなら SuspendHandler に委ねる。
        if (checkpoint.ActiveStates.Count > 0 || checkpoint.PendingWaits.Count > 0)
            return true;

        // この Fork 到達以降に Join が完了していれば、子待ち Unload の窓は閉じている。
        return checkpoint.Graph.Nodes.Any(n =>
            string.Equals(n.NodeType, "Join", StringComparison.OrdinalIgnoreCase)
            && n.StartedAt >= forkNode.StartedAt
            && (string.Equals(n.Fact, "Joined", StringComparison.OrdinalIgnoreCase)
                || n.CompletedAt is not null));
    }

    public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
        _projection.UpdateProjectionFromEngineAsync(
            executionId,
            ShouldDiscardRuntimeCheckpointAsync,
            DiscardOrRefreshRuntimeCheckpointAsync,
            _lifecycle.UpsertRuntimeCheckpointAsync,
            ct);

}
