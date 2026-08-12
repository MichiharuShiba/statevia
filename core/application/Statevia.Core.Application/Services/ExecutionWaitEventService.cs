using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// Wait / イベント適用（Publish / Resume）と子 Wait リダイレクト入口。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。HTTP 契約は変更しない。</para>
/// <para>物理子終端 <c>child.completed</c> の Join 再評価は Facade 側コールバックへ委譲する（Fork/Join 分離まで）。</para>
/// <para>checkpoint 寿命の破棄判定は Facade フックとして Resume 永続化時に受け取る。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="displayIds">表示 ID 解決。</param>
/// <param name="idGenerator">ID 生成。</param>
/// <param name="executions">executions 永続化。</param>
/// <param name="executionWaits">wait 行。</param>
/// <param name="mutationAuth">実行ミューテーション認可。</param>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="dedup">command_dedup。</param>
/// <param name="eventStore">event_store。</param>
/// <param name="eventDeliveryDedup">event_delivery_dedup。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="mutationPersistence">Serializable 永続化。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="idempotency">冪等サービス。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="lifecycle">ライフサイクル（Engine hydrate / checkpoint upsert）。</param>
/// <param name="forkChildCoordinator">Fork 子 Wait 配送（任意）。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionWaitEventService(
    IExecutionEngine engine,
    IDisplayIdService displayIds,
    IIdGenerator idGenerator,
    IExecutionRepository executions,
    IExecutionWaitRepository executionWaits,
    IExecutionMutationAuthorization mutationAuth,
    ITenantContextAccessor tenantContext,
    ICommandDedupRepository dedup,
    IEventStoreRepository eventStore,
    IEventDeliveryDedupRepository eventDeliveryDedup,
    ICoreTransactionExecutor executor,
    IExecutionMutationPersistence mutationPersistence,
    ILogger<ExecutionWaitEventService> logger,
    ExecutionIdempotencyService idempotency,
    ExecutionProjectionOrchestrator projection,
    ExecutionLifecycleCommandService lifecycle,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    /// <summary>HTTP 204 No Content。</summary>
    private const int HttpStatus204NoContent = 204;

    private static class ExecutionGraphJsonProperties
    {
        internal const string Nodes = "nodes";
        internal const string CompletedAt = "completedAt";
        internal const string NodeId = "nodeId";
        internal const string NodeType = "nodeType";
    }


    /// <summary>
    /// 名前付きイベントを Engine へ発行し、投影・Wait clear・delivery / dedup を永続化する。
    /// </summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="eventName">イベント名。</param>
    /// <param name="idempotencyKey">冪等キー（任意）。</param>
    /// <param name="requestContext">HTTP / Worker 文脈。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task PublishEventAsync(
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var tenantKey = tenantContext.GetRequiredTenantKey();
        var tenantId = tenantContext.GetRequiredTenantId();
        var dedupKey = idempotency.CreateKey(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

        if (await idempotency.HasValidCommandDedupAsync(dedupKey, ct).ConfigureAwait(false))
            return;

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        // 親 ID への PublishEvent も子 Wait へ振り替える（要件4）。
        if (await TryRedirectPublishEventToChildWaitAsync(
                uuid.Value,
                eventName,
                idempotencyKey,
                requestContext,
                ct).ConfigureAwait(false))
        {
            return;
        }

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await projection.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, idGenerator);

        if (await idempotency.TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        await lifecycle.EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        // PublishEvent 復帰時点ではグラフ上の Wait 完了が未反映になり得るため、発行前の nodeId で先行削除する。
        // グラフ解析失敗時は永続化済み wait 行からフォールバック解決する（Applied 時のみ）。
        var engineId = uuid.Value.ToString();
        var prePublishGraphJson = engine.ExportExecutionGraph(engineId);

        // PublishEvent シム条件外（複数 Wait / 複数 events 等）は設計どおり 422。
        var publishApply = InvokeEngineWaitMutation(
            () => engine.PublishEvent(engineId, eventName, clientEventId));
        var skipEventAppend = publishApply.IsAlreadyApplied;
        var nodeIdToClearFromGraph = publishApply.IsApplied
            ? TryResolveSoleActiveWaitNodeId(prePublishGraphJson)
            : null;
        await AwaitEngineReadyAfterWaitClearAsync(
                engineId,
                publishApply.IsApplied ? nodeIdToClearFromGraph : null,
                ct)
            .ConfigureAwait(false);

        var (pubStatus, pubCancel, pubGraphJson) = projection.BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName },
            JsonSerializerProfiles.CamelCase);

        await mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                var nodeIdToClear = await ResolvePublishNodeIdToClearAsync(
                        uow,
                        uuid.Value,
                        publishApply.IsApplied,
                        nodeIdToClearFromGraph,
                        eventName,
                        ctInner)
                    .ConfigureAwait(false);

                await executions
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, pubStatus, pubCancel, pubGraphJson, ctInner)
                    .ConfigureAwait(false);
                await projection.SyncOperationalProjectionAsync(
                        uow,
                        uuid.Value,
                        tenantId,
                        pubStatus,
                        pubGraphJson,
                        nodeIdToClear,
                        ctInner)
                    .ConfigureAwait(false);
                await AppendEventPublishedAsync(
                        uow,
                        uuid.Value,
                        clientEventId,
                        publishedPayload,
                        skipEventAppend,
                        ctInner)
                    .ConfigureAwait(false);

                if (dedupKey is { } saveKey)
                {
                    var now = DateTime.UtcNow;
                    await dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = HttpStatus204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        ctInner).ConfigureAwait(false);
                }

                var nowUtc = DateTime.UtcNow;
                await eventDeliveryDedup.TryUpdateStatusAsync(
                    uow,
                    tenantId,
                    uuid.Value,
                    clientEventId,
                    new EventDeliveryDedupStatusUpdate(
                        EventDeliveryDedupStatuses.Applied,
                        nowUtc,
                        AppliedAt: nowUtc,
                        ErrorCode: null),
                    ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        // 永続化中に進んだ Engine 完了を取りこぼさない（中間 RUNNING 上書きの保険）。
        if (publishApply.IsApplied)
            await projection.EnqueueAsync(uuid.Value, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Publish / Resume で Wait を消したあと、完了と後続 quiesce を待つ。
    /// </summary>
    private async Task AwaitEngineReadyAfterWaitClearAsync(
        string engineId,
        string? nodeIdToClear,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nodeIdToClear))
            return;

        await WaitForEngineWaitNodeCompletedAsync(engineId, nodeIdToClear, ct).ConfigureAwait(false);
        // Wait 完了直後の中間グラフ（次 Task RUNNING）を永続化すると完了投影を上書きし固着する。
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// グラフ解決できなかった場合に永続 wait 行から削除対象 nodeId を補完する。
    /// </summary>
    private async Task<string?> ResolvePublishNodeIdToClearAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        bool publishApplied,
        string? nodeIdFromGraph,
        string eventName,
        CancellationToken ct)
    {
        if (!publishApplied || !string.IsNullOrWhiteSpace(nodeIdFromGraph))
            return nodeIdFromGraph;

        var persistedWaits = await executionWaits
            .ListByExecutionIdAsync(uow, executionId, ct)
            .ConfigureAwait(false);
        return TryResolveWaitNodeIdToClearFromPersisted(persistedWaits, eventName);
    }

    /// <summary>EventPublished を通常 append または clientEvent 冪等 append する。</summary>
    private async Task AppendEventPublishedAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        string publishedPayload,
        bool skipEventAppend,
        CancellationToken ct)
    {
        if (!skipEventAppend)
        {
            await eventStore
                .AppendAsync(uow, executionId, EventStoreEventType.EventPublished, publishedPayload, ct)
                .ConfigureAwait(false);
            return;
        }

        await eventStore.TryAppendIfAbsentByClientEventAsync(
            uow,
            executionId,
            clientEventId,
            EventStoreEventType.EventPublished,
            publishedPayload,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Wait ノードを Resume する。子 Wait リダイレクトと <c>child.completed</c> Join コールバックを含む。
    /// </summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="nodeId">Wait ノード ID。</param>
    /// <param name="resumeKey">Resume イベント名。</param>
    /// <param name="idempotencyKey">冪等キー（任意）。</param>
    /// <param name="requestContext">HTTP / Worker 文脈。</param>
    /// <param name="handleChildCompletedResume">物理子終端時の Join 再評価（Facade）。</param>
    /// <param name="shouldDiscardRuntimeCheckpoint">checkpoint 破棄候補判定（Facade）。</param>
    /// <param name="discardOrRefreshRuntimeCheckpoint">checkpoint 破棄または所有中更新（Facade）。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        Func<Guid, ExecutionRow, string, CancellationToken, Task> handleChildCompletedResume,
        Func<Guid, string, string, CancellationToken, Task<bool>> shouldDiscardRuntimeCheckpoint,
        Func<ICoreUnitOfWork, string, Guid, CancellationToken, Task<bool>> discardOrRefreshRuntimeCheckpoint,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(handleChildCompletedResume);
        ArgumentNullException.ThrowIfNull(shouldDiscardRuntimeCheckpoint);
        ArgumentNullException.ThrowIfNull(discardOrRefreshRuntimeCheckpoint);

        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resumeKey);

        var eventName = resumeKey.Trim();
        var tenantKey = tenantContext.GetRequiredTenantKey();
        var tenantId = tenantContext.GetRequiredTenantId();
        var dedupKey = idempotency.CreateKey(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

        if (await idempotency.HasValidCommandDedupAsync(dedupKey, ct).ConfigureAwait(false))
            return;

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        // 親 ID へ届いた分岐 Wait は子 execution へ振り替える（要件4）。
        if (await TryRedirectResumeToChildWaitAsync(
                uuid.Value,
                nodeId,
                eventName,
                idempotencyKey,
                requestContext,
                handleChildCompletedResume,
                shouldDiscardRuntimeCheckpoint,
                discardOrRefreshRuntimeCheckpoint,
                ct).ConfigureAwait(false))
        {
            return;
        }

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await projection.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, idGenerator);

        if (await idempotency.TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        // 物理子終端の予約イベントは Wait Resume ではなく Join 再評価へ振る（D3）。
        if (string.Equals(eventName, ExecutionWaitEventNames.ChildCompleted, StringComparison.Ordinal))
        {
            await handleChildCompletedResume(uuid.Value, execution, nodeId, ct).ConfigureAwait(false);
            return;
        }

        await lifecycle.EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        // Resume 正本: nodeId + eventName。wait 行は nodeId で先行削除する。
        // 許可外イベント・非アクティブ Wait などは 422。
        var engineId = uuid.Value.ToString();
        InvokeEngineWaitMutation(() => engine.ResumeWaitNode(engineId, nodeId, eventName));
        // Resume のグラフ完了は非同期継続のため、投影取得前に Wait 完了を待つ。
        await WaitForEngineWaitNodeCompletedAsync(engineId, nodeId, ct).ConfigureAwait(false);
        // 続けて後続 Task が落ち着くまで待つ（Wait 完了直後の RUNNING 中間像の永続化を避ける）。
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        // Again → Fork の Suspend Unload 後は in-process snapshot が欠ける。SuspendHandler 投影を正とする。
        var (engineStillLoaded, pubStatus, pubCancel, pubGraphJson) =
            await ResolveProjectionAfterWaitResumeAsync(uuid.Value, ct).ConfigureAwait(false);

        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName, nodeId },
            JsonSerializerProfiles.CamelCase);

        await PersistWaitResumeSideEffectsAsync(
                new WaitResumePersistRequest(
                    tenantId,
                    uuid.Value,
                    engineId,
                    nodeId,
                    clientEventId,
                    dedupKey,
                    engineStillLoaded,
                    pubStatus,
                    pubCancel,
                    pubGraphJson,
                    publishedPayload),
                shouldDiscardRuntimeCheckpoint,
                discardOrRefreshRuntimeCheckpoint,
                ct)
            .ConfigureAwait(false);

        // 永続化中に進んだ Engine 完了を取りこぼさない（中間 RUNNING 上書きの保険）。
        await projection.EnqueueAsync(uuid.Value, ct).ConfigureAwait(false);
    }

    /// <summary>Wait Resume 後の投影・checkpoint・event_store・dedup 永続化入力。</summary>
    private readonly record struct WaitResumePersistRequest(
        Guid TenantId,
        Guid ExecutionId,
        string EngineId,
        string NodeId,
        Guid ClientEventId,
        CommandDedupKey? DedupKey,
        bool EngineStillLoaded,
        string PubStatus,
        bool? PubCancel,
        string PubGraphJson,
        string PublishedPayload);

    /// <summary>Wait Resume 後の投影・checkpoint・event_store・dedup を同一 tx で永続化する。</summary>
    private async Task PersistWaitResumeSideEffectsAsync(
        WaitResumePersistRequest request,
        Func<Guid, string, string, CancellationToken, Task<bool>> shouldDiscardRuntimeCheckpoint,
        Func<ICoreUnitOfWork, string, Guid, CancellationToken, Task<bool>> discardOrRefreshRuntimeCheckpoint,
        CancellationToken ct)
    {
        await mutationPersistence.ExecuteSerializableWithRetryAsync(
            request.TenantId,
            request.ExecutionId,
            request.ClientEventId,
            async (uow, ctInner) =>
            {
                await executions
                    .UpdateExecutionAndSnapshotAsync(
                        uow,
                        request.ExecutionId,
                        request.PubStatus,
                        request.PubCancel,
                        request.PubGraphJson,
                        ctInner)
                    .ConfigureAwait(false);
                await projection.SyncOperationalProjectionAsync(
                        uow,
                        request.ExecutionId,
                        request.TenantId,
                        request.PubStatus,
                        request.PubGraphJson,
                        nodeIdToClear: request.NodeId,
                        ctInner)
                    .ConfigureAwait(false);

                if (request.EngineStillLoaded)
                {
                    if (await shouldDiscardRuntimeCheckpoint(
                            request.ExecutionId,
                            request.PubStatus,
                            request.PubGraphJson,
                            ctInner)
                        .ConfigureAwait(false))
                    {
                        await discardOrRefreshRuntimeCheckpoint(
                                uow,
                                request.EngineId,
                                request.ExecutionId,
                                ctInner)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await lifecycle.UpsertRuntimeCheckpointAsync(
                                uow,
                                request.EngineId,
                                request.ExecutionId,
                                ctInner)
                            .ConfigureAwait(false);
                    }
                }

                await eventStore
                    .AppendAsync(
                        uow,
                        request.ExecutionId,
                        EventStoreEventType.EventPublished,
                        request.PublishedPayload,
                        ctInner)
                    .ConfigureAwait(false);

                if (request.DedupKey is { } saveKey)
                {
                    var now = DateTime.UtcNow;
                    await dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = HttpStatus204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        ctInner).ConfigureAwait(false);
                }

                var nowUtc = DateTime.UtcNow;
                await eventDeliveryDedup.TryUpdateStatusAsync(
                    uow,
                    request.TenantId,
                    request.ExecutionId,
                    request.ClientEventId,
                    new EventDeliveryDedupStatusUpdate(
                        EventDeliveryDedupStatuses.Applied,
                        nowUtc,
                        AppliedAt: nowUtc,
                        ErrorCode: null),
                    ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Wait Resume 後の投影断面を Engine または DB（Unload 済み）から解決する。
    /// </summary>
    /// <returns>
    /// Engine がメモリ上に残っているとき <c>engineStillLoaded</c> は true。
    /// </returns>
    private async Task<(bool EngineStillLoaded, string Status, bool? CancelRequested, string GraphJson)>
        ResolveProjectionAfterWaitResumeAsync(Guid executionId, CancellationToken ct)
    {
        var (projectedStatus, projectedCancel, projectedGraphJson) = projection.BuildProjectionFromEngineForQueue(executionId);
        if (projectedStatus is not null && projectedGraphJson is not null)
        {
            return (true, projectedStatus, projectedCancel, projectedGraphJson);
        }

        var unloadedSnapshot = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => executions.GetSnapshotByExecutionIdAsync(uow, executionId, innerCt),
                ct)
            .ConfigureAwait(false);
        var unloadedRow = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => executions.GetByExecutionIdAsync(uow, executionId, innerCt),
                ct)
            .ConfigureAwait(false);
        if (unloadedSnapshot is null
            || unloadedRow is null
            || string.IsNullOrWhiteSpace(unloadedSnapshot.GraphJson))
        {
            throw new InvalidOperationException(
                $"Cannot project execution '{executionId:D}': engine snapshot is missing after Resume unload.");
        }

        return (false, unloadedRow.Status, unloadedRow.CancelRequested, unloadedSnapshot.GraphJson);
    }

    /// <summary>
    /// 親 ID の Resume を子 Wait へ振り替える。振り替えたとき true。
    /// </summary>
    private async Task<bool> TryRedirectResumeToChildWaitAsync(
        Guid requestedExecutionId,
        string nodeId,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        Func<Guid, ExecutionRow, string, CancellationToken, Task> handleChildCompletedResume,
        Func<Guid, string, string, CancellationToken, Task<bool>> shouldDiscardRuntimeCheckpoint,
        Func<ICoreUnitOfWork, string, Guid, CancellationToken, Task<bool>> discardOrRefreshRuntimeCheckpoint,
        CancellationToken ct)
    {
        if (forkChildCoordinator is null)
            return false;

        if (string.Equals(eventName, ExecutionWaitEventNames.ChildCompleted, StringComparison.Ordinal))
            return false;

        var delivery = await forkChildCoordinator
            .TryResolveChildWaitDeliveryAsync(requestedExecutionId, nodeId, eventName, ct)
            .ConfigureAwait(false);
        if (delivery is null)
            return false;

        await ResumeNodeAsync(
                delivery.ExecutionId.ToString("D"),
                delivery.NodeId,
                delivery.EventName,
                idempotencyKey,
                requestContext,
                handleChildCompletedResume,
                shouldDiscardRuntimeCheckpoint,
                discardOrRefreshRuntimeCheckpoint,
                ct)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 親 ID の PublishEvent を子 Wait へ振り替える。振り替えたとき true。
    /// </summary>
    private async Task<bool> TryRedirectPublishEventToChildWaitAsync(
        Guid requestedExecutionId,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        if (forkChildCoordinator is null)
            return false;

        var delivery = await forkChildCoordinator
            .TryResolveChildWaitDeliveryAsync(requestedExecutionId, nodeId: null, eventName, ct)
            .ConfigureAwait(false);
        if (delivery is null)
            return false;

        await PublishEventAsync(
                delivery.ExecutionId.ToString("D"),
                delivery.EventName,
                idempotencyKey,
                requestContext,
                ct)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Resume 適用後、対象 Wait ノードのグラフ完了が反映されるまで短く待つ。
    /// </summary>
    /// <remarks>
    /// Engine 側の完了処理は <c>RunContinuationsAsynchronously</c> のため、
    /// <see cref="IExecutionEngine.ResumeWaitNode"/> 復帰直後の投影は古い Wait を含み得る。
    /// </remarks>
    private async Task WaitForEngineWaitNodeCompletedAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct)
    {
        const int maxAttempts = 200;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (engine.GetSnapshot(engineExecutionId) is null)
                return;

            var graphJson = engine.ExportExecutionGraph(engineExecutionId);
            if (!TryGetGraphNodeCompletion(graphJson, nodeId, out var completed))
            {
                // ノードが無い（テスト用 stub 等）場合は待たない。
                return;
            }

            if (completed)
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        logger.WaitNodeCompletionTimedOut(nodeId, engineExecutionId);
    }

    /// <summary>
    /// Wait 再開後、後続状態の実行が落ち着くまで短く待つ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WaitForEngineWaitNodeCompletedAsync"/> は Wait の <c>completedAt</c> 時点で戻る。
    /// その直後は <c>ProcessFact</c> → 次 Task の <c>AddNode</c> が走り、グラフは
    /// 「Wait SUCCEEDED + 次 Task RUNNING」の中間像になり得る。
    /// </para>
    /// <para>
    /// Resume / Publish がこの中間像を serializable トランザクションで永続化すると、
    /// 後続の完了投影（noop 完了 → Completed）を上書きし、次ノードが RUNNING のまま固着する。
    /// </para>
    /// </remarks>
    public async Task WaitForEngineQuiescedAfterWaitAsync(
        string engineExecutionId,
        CancellationToken ct)
    {
        const int maxAttempts = 200;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = engine.GetSnapshot(engineExecutionId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
                return;

            // ContinueRestoredWait は次 Task を ActiveStates に載せてから Wait を外すため、
            // 後続実行中は Count > 0。空なら後続未起動または完了済み。
            if (snapshot.ActiveStates.Count == 0)
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        logger.WaitResumeQuiesceTimedOut(engineExecutionId);
    }

    /// <summary>グラフ JSON 上の指定 nodeId の完了有無を取得する。</summary>
    /// <returns>ノードが存在するとき <see langword="true"/>。</returns>
    internal static bool TryGetGraphNodeCompletion(string graphJson, string nodeId, out bool completed)
    {
        completed = false;
        if (string.IsNullOrWhiteSpace(graphJson) || string.IsNullOrWhiteSpace(nodeId))
            return false;

        try
        {
            using var document = JsonDocument.Parse(graphJson);
            if (!document.RootElement.TryGetProperty(ExecutionGraphJsonProperties.Nodes, out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty(ExecutionGraphJsonProperties.NodeId, out var idProp)
                    || idProp.ValueKind != JsonValueKind.String
                    || !string.Equals(idProp.GetString(), nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                completed = node.TryGetProperty(ExecutionGraphJsonProperties.CompletedAt, out var completedAt)
                    && completedAt.ValueKind is not JsonValueKind.Null
                    && completedAt.ValueKind is not JsonValueKind.Undefined;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Engine Wait 操作の <see cref="InvalidOperationException"/> を API 422 に写像する。
    /// </summary>
    /// <remarks>
    /// PublishEvent シム条件外・Resume の許可外イベントなどは Engine が
    /// <see cref="InvalidOperationException"/> を投げる。クライアント向けには
    /// <see cref="ApiValidationException"/>（422）へ変換する。
    /// </remarks>
    private static T InvokeEngineWaitMutation<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiValidationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Engine Wait 操作の <see cref="InvalidOperationException"/> を API 422 に写像する。
    /// </summary>
    private static void InvokeEngineWaitMutation(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiValidationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// PublishEvent シム用。グラフ上の未完了 Wait がちょうど 1 件ならその nodeId を返す。
    /// </summary>
    /// <remarks>
    /// Engine の <c>PublishEvent</c> は単一アクティブ Wait のみ許可する。投影同期では
    /// 復帰直後にグラフ完了が遅延し得るため、発行前に取得した nodeId で wait 行を先行削除する。
    /// </remarks>
    /// <param name="graphJson"><see cref="IExecutionEngine.ExportExecutionGraph"/> の JSON。</param>
    /// <returns>唯一のアクティブ Wait の nodeId。0 件または 2 件以上なら null。</returns>
    private static string? TryResolveSoleActiveWaitNodeId(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(graphJson);
            if (!doc.RootElement.TryGetProperty(ExecutionGraphJsonProperties.Nodes, out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return FindSoleActiveWaitNodeId(nodes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>nodes 配列から唯一の未完了 Wait nodeId を返す（0 件または複数は null）。</summary>
    private static string? FindSoleActiveWaitNodeId(JsonElement nodes)
    {
        string? soleNodeId = null;
        foreach (var node in nodes.EnumerateArray())
        {
            if (!TryGetActiveWaitNodeId(node, out var nodeId))
                continue;

            if (soleNodeId is not null)
                return null;

            soleNodeId = nodeId;
        }

        return soleNodeId;
    }

    /// <summary>未完了 Wait ノードなら nodeId を取り出す。</summary>
    /// <remarks>
    /// 許可イベント未設定の Wait は durable wait 投影対象外のため除外する
    ///（<c>allowedEvents</c> または互換の <c>waitKey</c> が必要）。
    /// </remarks>
    private static bool TryGetActiveWaitNodeId(JsonElement node, out string? nodeId)
    {
        nodeId = null;
        if (!node.TryGetProperty(ExecutionGraphJsonProperties.NodeType, out var nodeType)
            || !string.Equals(nodeType.GetString(), "Wait", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.TryGetProperty(ExecutionGraphJsonProperties.CompletedAt, out var completedAt)
            && completedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return false;
        }

        if (!HasConfiguredWaitEvents(node))
            return false;

        if (!node.TryGetProperty(ExecutionGraphJsonProperties.NodeId, out var nodeIdElement))
            return false;

        var value = nodeIdElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        nodeId = value;
        return true;
    }

    /// <summary>
    /// グラフ JSON の Wait ノードに許可イベント設定があるか
    ///（<c>allowedEvents</c> 優先、なければ非空の <c>waitKey</c>）。
    /// </summary>
    private static bool HasConfiguredWaitEvents(JsonElement node)
    {
        if (node.TryGetProperty("allowedEvents", out var allowedEvents)
            && allowedEvents.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in allowedEvents.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(entry.GetString()))
                {
                    return true;
                }
            }
        }

        return node.TryGetProperty("waitKey", out var waitKey)
            && waitKey.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(waitKey.GetString());
    }

    /// <summary>
    /// グラフから nodeId を解決できないときのフォールバック。永続化済み wait から削除対象を決める。
    /// </summary>
    /// <param name="waits">当該 execution の wait 行。</param>
    /// <param name="eventName">Publish したイベント名（前後空白は Trim して照合）。</param>
    /// <returns>
    /// wait が 1 件ならその nodeId。複数なら <paramref name="eventName"/> を許可する行がちょうど 1 件のときその nodeId。
    /// それ以外は null。
    /// </returns>
    internal static string? TryResolveWaitNodeIdToClearFromPersisted(
        IReadOnlyList<ExecutionWaitRow> waits,
        string eventName)
    {
        if (waits.Count == 0)
            return null;

        if (waits.Count == 1)
            return waits[0].NodeId;

        // AllowedEvents は投影時に Trim 済み。Publish 側の前後空白とも揃える。
        var normalizedEventName = eventName.Trim();
        string? soleMatchingNodeId = null;
        foreach (var wait in waits)
        {
            var allowsEvent = wait.AllowedEvents.Any(e =>
                string.Equals(e, normalizedEventName, StringComparison.OrdinalIgnoreCase));
            if (!allowsEvent)
                continue;

            if (soleMatchingNodeId is not null)
                return null;

            soleMatchingNodeId = wait.NodeId;
        }

        return soleMatchingNodeId;
    }

    private Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }

}
