using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 親 Physical Join の完了/失敗と子完了 Resume による Join 再評価。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade / <see cref="ExecutionWaitEventService"/> から利用する。</para>
/// <para>展開・子 Wait 配送・EvaluateJoin 自体は既存の <see cref="IForkChildExecutionCoordinator"/> を利用する。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="executions">executions / snapshot 永続化。</param>
/// <param name="checkpointStore">runtime checkpoint。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="lifecycle">checkpoint upsert。</param>
/// <param name="engineSession">Engine Load / hydrate。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="checkpoints">checkpoint 寿命・破棄。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="forkChildCoordinator">Join 評価・展開協調（任意）。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionForkJoinCoordinator(
    IExecutionEngine engine,
    IExecutionRepository executions,
    IExecutionCheckpointStore checkpointStore,
    ICoreTransactionExecutor executor,
    ExecutionLifecycleCommandService lifecycle,
    ExecutionEngineSession engineSession,
    ExecutionProjectionOrchestrator projection,
    ExecutionCheckpointService checkpoints,
    ILogger<ExecutionForkJoinCoordinator> logger,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    /// <summary>実行グラフ JSON のプロパティ名（Join 冪等判定用）。</summary>
    private static class ExecutionGraphJsonProperties
    {
        internal const string Nodes = "nodes";
        internal const string CompletedAt = "completedAt";
        internal const string NodeId = "nodeId";
        internal const string Fact = "fact";
        internal const string Status = "status";
    }

    /// <summary>
    /// 物理子終端の予約 Resume を受け、親の Join を再評価する。Join 待ち中の親は Unload する。
    /// </summary>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="execution">親の投影行。</param>
    /// <param name="forkNodeId">Resume payload の nodeId（Fork 到達インスタンス）。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task HandleChildCompletedResumeAsync(
        Guid parentExecutionId,
        ExecutionRow execution,
        string forkNodeId,
        CancellationToken ct)
    {
        if (forkChildCoordinator is null)
            return;

        if (ExecutionLifecycleCommandService.IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        var evaluation = await forkChildCoordinator
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
                var snapshotRow = await executor.ExecuteReadOnlyAsync(
                        (uow, innerCt) => executions.GetSnapshotByExecutionIdAsync(
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
                    logger.ForkJoinAlreadyCompleted(parentExecutionId, forkNodeId, evaluation.JoinState);
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
        var document = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => checkpointStore.GetByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        if (document is null
            || !ExecutionEngineSession.TryParseRuntimeCheckpoint(document.CheckpointJson, out var checkpoint, out _))
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

    /// <summary>Join 失敗／キャンセルを親投影へ伝播する（残子 Cancel は親 Cancel カスケードと分岐 Wait の子配送）。</summary>
    private async Task FailParentFromJoinAsync(
        Guid parentExecutionId,
        string forkNodeId,
        string failureStatus,
        CancellationToken ct)
    {
        logger.ForkJoinFailedPropagated(parentExecutionId, forkNodeId, failureStatus);

        var snapshot = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => executions.GetSnapshotByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        var graphJson = snapshot?.GraphJson ?? """{"nodes":[],"edges":[]}""";
        var cancelRequested = string.Equals(
            failureStatus,
            ExecutionProjectionStatuses.Cancelled,
            StringComparison.Ordinal);

        await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    await executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            failureStatus,
                            cancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);

                    await checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            parentExecutionId.ToString(),
                            parentExecutionId,
                            innerCt)
                        .ConfigureAwait(false);
                },
                ct)
            .ConfigureAwait(false);

        engine.Unload(parentExecutionId.ToString());
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
        logger.ForkJoinSatisfied(parentExecutionId, forkNodeId, evaluation.JoinState);

        await engineSession.EnsureEngineRuntimeLoadedForMutationAsync(parentExecutionId, execution, ct).ConfigureAwait(false);

        var engineId = parentExecutionId.ToString();
        IReadOnlyList<PhysicalJoinContextFragment>? fragments = null;
        if (evaluation.ContextMerges is { Count: > 0 })
        {
            fragments = evaluation.ContextMerges
                .Select(m => new PhysicalJoinContextFragment(m.States, m.Vars))
                .ToList();
        }

        engine.CompletePhysicalJoin(
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
        var (status, cancelRequested, graphJson) = projection.BuildProjectionFromEngineForQueue(parentExecutionId);
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

            logger.ForkJoinProjectionSkippedAfterUnload(
                parentExecutionId,
                forkNodeId,
                evaluation.JoinState);
            return;
        }

        await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    // Settlement 待機後〜tx 開始前に Wait Persist が進んでいることがあるので直前に再取得。
                    var (freshStatus, freshCancel, freshGraph) = projection.BuildProjectionFromEngineForQueue(parentExecutionId);
                    var writeStatus = freshStatus ?? status;
                    var writeCancel = freshCancel ?? cancelRequested;
                    var writeGraph = freshGraph ?? graphJson;

                    await executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            writeStatus,
                            writeCancel,
                            writeGraph,
                            innerCt)
                        .ConfigureAwait(false);
                    await projection.SyncOperationalProjectionAsync(
                            uow,
                            parentExecutionId,
                            execution.TenantId,
                            writeStatus,
                            writeGraph,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    if (await checkpoints.ShouldDiscardRuntimeCheckpointAsync(
                                parentExecutionId,
                                writeStatus,
                                writeGraph,
                                innerCt)
                            .ConfigureAwait(false))
                    {
                        await checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                                uow,
                                engineId,
                                parentExecutionId,
                                innerCt)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await lifecycle.UpsertRuntimeCheckpointAsync(uow, engineId, parentExecutionId, innerCt)
                            .ConfigureAwait(false);
                    }
                },
                ct)
            .ConfigureAwait(false);

        await projection.EnqueueAsync(parentExecutionId, ct).ConfigureAwait(false);
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
            var snapshot = engine.GetSnapshot(engineExecutionId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
                return;

            var graphJson = engine.ExportExecutionGraph(engineExecutionId);
            if (IsPhysicalJoinAlreadyCompleted(graphJson, forkNodeId, joinState))
                return;

            var checkpoint = engine.ExportCheckpoint(engineExecutionId);
            if (checkpoint is { PendingWaits.Count: > 0 })
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        logger.WaitResumeQuiesceTimedOut(engineExecutionId);
    }

    /// <summary>
    /// Unload 後に runtime checkpoint が Wait 断面まで進んでいるとき、graph snapshot を追いつかせる。
    /// </summary>
    private async Task TrySyncGraphSnapshotFromRuntimeCheckpointAsync(
        Guid executionId,
        ExecutionRow execution,
        CancellationToken ct)
    {
        await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var runtime = await checkpointStore
                        .GetByExecutionIdAsync(uow, executionId, innerCt)
                        .ConfigureAwait(false);
                    if (runtime is null
                        || !ExecutionEngineSession.TryParseRuntimeCheckpoint(runtime.CheckpointJson, out var checkpoint, out _)
                        || checkpoint.PendingWaits.Count == 0)
                    {
                        return;
                    }

                    var graphJson = engine.ExportExecutionGraph(executionId.ToString());
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

                    var existingSnapshot = await executions
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

                    await executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            executionId,
                            status,
                            execution.CancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);
                    await projection.SyncOperationalProjectionAsync(
                            uow,
                            executionId,
                            execution.TenantId,
                            status,
                            graphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    logger.SyncedGraphSnapshotFromRuntimeCheckpoint(
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

}
