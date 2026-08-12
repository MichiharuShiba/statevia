using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行 runtime checkpoint の永続化と Unload / KeepLoaded、世代フェンス。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。HTTP / Worker 契約は変更しない。</para>
/// <para>同一実行の Persist+Unload は <c>PersistUnloadGates</c> で直列化し、古い Fork Unload の上書きを防ぐ。</para>
/// <para>寿命表に基づく破棄判定もここに集約し、投影 / Wait Resume からフックとして利用する。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="checkpointStore">runtime checkpoint 永続化。</param>
/// <param name="ownership">Worker 所有トラッカー。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="executions">executions / snapshot 永続化。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="lifecycle">ライフサイクル（checkpoint upsert）。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="forkChildCoordinator">物理 Join 保留判定（任意）。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionCheckpointService(
    IExecutionEngine engine,
    IExecutionCheckpointStore checkpointStore,
    ExecutionOwnershipTracker ownership,
    ICoreTransactionExecutor executor,
    IExecutionRepository executions,
    ExecutionProjectionOrchestrator projection,
    ExecutionLifecycleCommandService lifecycle,
    ILogger<ExecutionCheckpointService> logger,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    /// <summary>
    /// 同一実行の <see cref="PersistCheckpointAndUnloadCoreAsync"/> を直列化し、
    /// 古い Fork Unload が新しい Wait 断面を上書きしないようにする。
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PersistUnloadGates = new();

    /// <summary>
    /// ステップ完了時に checkpoint を保存する（Unload しない）。
    /// </summary>
    /// <remarks>
    /// Worker 所有中は世代付き upsert（fencing 失敗時はログのみ）。所有外は通常 upsert。
    /// Engine ID が GUID でない、または export が null のときは no-op。
    /// </remarks>
    /// <param name="engineExecutionId">Engine 辞書キー。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineExecutionId);
        if (!Guid.TryParse(engineExecutionId, out var executionId))
        {
            logger.SkipKeepLoadedCheckpointInvalidExecutionId(engineExecutionId);
            return;
        }

        var checkpoint = engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
            return;

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;

        await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (ownership.TryGet(executionId, out _, out var generation))
                {
                    var ok = await checkpointStore.TryUpsertRuntimeWithGenerationAsync(
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
                        logger.KeepLoadedCheckpointRejectedByFencing(executionId);
                    }

                    return;
                }

                await checkpointStore.UpsertAsync(
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

    /// <summary>
    /// 寿命表に従い checkpoint <b>内容</b>の破棄候補か。
    /// </summary>
    /// <remarks>
    /// 保持理由は <see cref="RuntimeCheckpointRetainReasons"/> のみ
    ///（PendingWaits / PendingPhysicalJoin）。
    /// Worker 所有 lease は同列にせず、破棄候補でも
    /// <see cref="DiscardOrRefreshRuntimeCheckpointAsync"/> が Delete を抑止する。
    /// </remarks>
    public async Task<bool> ShouldDiscardRuntimeCheckpointAsync(
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

        if (forkChildCoordinator is not null
            && await forkChildCoordinator
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
    public async Task<bool> DiscardOrRefreshRuntimeCheckpointAsync(
        ICoreUnitOfWork uow,
        string engineExecutionId,
        Guid executionId,
        CancellationToken ct)
    {
        if (ownership.TryGet(executionId, out _, out _))
        {
            // Worker 所有中。寿命表上は破棄候補でも lease 行は残し runtime だけ更新する。
            _ = await lifecycle.UpsertRuntimeCheckpointAsync(uow, engineExecutionId, executionId, ct)
                .ConfigureAwait(false);
            return false;
        }

        await checkpointStore.DeleteAsync(uow, executionId, ct).ConfigureAwait(false);
        return false;
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
            logger.SkipCheckpointPersistInvalidExecutionId(engineExecutionId);
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
        var checkpoint = engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
        {
            logger.ExportCheckpointNullSkipUnload(engineExecutionId, nodeId);
            return;
        }

        // ForkExpansion は fire-and-forget のため ExpandFork 完了が Join→decide より遅れることがある。
        // その遅延 Persist が空断面で Unload すると Wait 登録前／登録直後の decide を潰す（初回 Wait 到達不能）。
        if (IsForkExpansionUnloadObsolete(checkpoint, nodeId))
        {
            logger.SkipForkExpansionUnloadAlreadyProgressed(
                executionId,
                nodeId,
                checkpoint.ActiveStates.Count,
                checkpoint.PendingWaits.Count);
            return;
        }

        // Unload 前に投影へ反映する（Unload 後は GetSnapshot が null になり投影キューが空振りする）。
        // snapshot 欠落時は Unknown/`{}` を書かず checkpoint のみ残す（マルチ Worker 競合で UI を壊さない）。
        var snapshot = engine.GetSnapshot(engineExecutionId);
        string? status = null;
        bool? cancelRequested = null;
        string? graphJson = null;
        if (snapshot is not null)
        {
            status = ExecutionProjectionOrchestrator.MapStatus(snapshot);
            cancelRequested = snapshot.IsCancelled;
            graphJson = engine.ExportExecutionGraph(engineExecutionId);
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

        ownership.Clear(executionId);
        engine.Unload(engineExecutionId);

        if (fenceLost)
        {
            logger.UnloadAfterWaitFencingLost(nodeId, executionId);
            return;
        }

        logger.CheckpointUnloadedAfterWait(executionId, nodeId);
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
        var fenceLost = await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var existingCheckpoint = await checkpointStore
                    .GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (existingCheckpoint is not null
                    && ExecutionLifecycleCommandService.TryParseRuntimeCheckpoint(existingCheckpoint.CheckpointJson, out var stored, out _)
                    && IsRuntimeCheckpointLessAdvanced(request.Checkpoint, stored))
                {
                    skippedStalePersist = true;
                    logger.SkipStaleCheckpointPersist(
                        request.ExecutionId,
                        request.NodeId,
                        stored.PendingWaits.Count,
                        request.Checkpoint.PendingWaits.Count,
                        stored.Graph.Nodes.Count,
                        request.Checkpoint.Graph.Nodes.Count);
                    return false;
                }

                var execution = await executions.GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (execution is not null && request.Status is not null && request.GraphJson is not null)
                {
                    await executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            request.ExecutionId,
                            request.Status,
                            request.CancelRequested,
                            request.GraphJson,
                            innerCt)
                        .ConfigureAwait(false);
                    await projection.SyncOperationalProjectionAsync(
                            uow,
                            request.ExecutionId,
                            execution.TenantId,
                            request.Status,
                            request.GraphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);
                }

                if (ownership.TryGet(request.ExecutionId, out _, out var generation))
                {
                    var upserted = await checkpointStore.TryUpsertRuntimeWithGenerationAsync(
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

                    await checkpointStore.TryClearOwnershipAsync(uow, request.ExecutionId, generation, innerCt)
                        .ConfigureAwait(false);
                    return false;
                }

                await checkpointStore.UpsertAsync(
                    uow,
                    new ExecutionCheckpointDocument
                    {
                        ExecutionId = request.ExecutionId,
                        CheckpointJson = request.CheckpointJson,
                        SchemaVersion = request.Checkpoint.SchemaVersion,
                        UpdatedAt = request.UpdatedAt
                    },
                    innerCt).ConfigureAwait(false);

                var existing = await checkpointStore.GetByExecutionIdAsync(uow, request.ExecutionId, innerCt)
                    .ConfigureAwait(false);
                if (existing?.OwnerWorkerId is not null)
                {
                    await checkpointStore.TryClearOwnershipAsync(
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

}
