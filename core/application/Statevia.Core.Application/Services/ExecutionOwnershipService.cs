using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// Worker 所有セッションとローカル Engine Load 待機。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。lease 獲得・更新・解放と AwaitLocal を担う。</para>
/// <para>AwaitLocal の終端時投影は <see cref="ExecutionProjectionOrchestrator"/> と checkpoint 寿命フックを利用する。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="checkpointStore">runtime checkpoint / ownership 永続化。</param>
/// <param name="ownership">プロセス内所有トラッカー。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="checkpoints">checkpoint 寿命・Persist+Unload。</param>
/// <param name="lifecycle">checkpoint upsert。</param>
/// <param name="logger">構造化ログ。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionOwnershipService(
    IExecutionEngine engine,
    IExecutionCheckpointStore checkpointStore,
    ExecutionOwnershipTracker ownership,
    ICoreTransactionExecutor executor,
    ExecutionProjectionOrchestrator projection,
    ExecutionCheckpointService checkpoints,
    ExecutionLifecycleCommandService lifecycle,
    ILogger<ExecutionOwnershipService> logger)
{
    /// <summary>Worker が 1 Load を待つときのポーリング間隔。</summary>
    private static readonly TimeSpan LocalExecutionLoadPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>実行グラフ JSON のプロパティ名（AwaitLocal の実行中ノード判定用）。</summary>
    private static class ExecutionGraphJsonProperties
    {
        internal const string Nodes = "nodes";
        internal const string CompletedAt = "completedAt";
        internal const string StartedAt = "startedAt";
        internal const string NodeType = "nodeType";
    }

    /// <summary>
    /// Worker セッションの所有を獲得しトラッカーへ登録する。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="workerId">Worker 識別子。</param>
    /// <param name="leaseDuration">lease 期間。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>獲得後の世代。失敗時は null。</returns>
    public async Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        var generation = await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var seed = new ExecutionCheckpointDocument
                {
                    ExecutionId = executionId,
                    CheckpointJson = "{}",
                    SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                    UpdatedAt = DateTime.UtcNow
                };
                return await checkpointStore.TryAcquireOwnershipAsync(
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
            ownership.Set(executionId, workerId, gen);

        return generation;
    }

    /// <summary>
    /// 所有 lease を延長する。トラッカー空（Unload 済み）なら成功扱いで no-op。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="leaseDuration">延長期間。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>延長成功またはトラッカー空のとき true。</returns>
    public async Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        // Wait Unload 済みでトラッカーが空なら延長不要（失敗扱いにすると heartbeat が誤キャンセルする）。
        if (!ownership.TryGet(executionId, out _, out var generation))
            return true;

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        return await executor.ExecuteReadCommittedAsync(
            (uow, innerCt) => checkpointStore.TryRenewOwnershipLeaseAsync(
                uow,
                executionId,
                generation,
                leaseUntil,
                innerCt),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 所有を解放しトラッカーから除去する。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct)
    {
        if (!ownership.TryGet(executionId, out _, out var generation))
            return;

        try
        {
            await executor.ExecuteReadCommittedAsync(
                (uow, innerCt) => checkpointStore.TryClearOwnershipAsync(
                    uow,
                    executionId,
                    generation,
                    innerCt),
                ct).ConfigureAwait(false);
        }
        finally
        {
            ownership.Clear(executionId);
        }
    }

    /// <summary>
    /// ローカル所有をベストエフォートで放棄し Engine を Unload する。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    public Task AbandonLocalOwnedSessionAsync(Guid executionId)
    {
        ownership.Clear(executionId);
        var engineId = executionId.ToString();
        try
        {
            engine.Unload(engineId);
        }
#pragma warning disable CA1031 // ローカル放棄はベストエフォート。Unload 失敗で Worker を止めない。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.UnloadDuringAbandon(exception, executionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ローカル Engine が終端または durable Wait まで進むのを待ち、必要なら投影・Unload する。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="noProgressTimeout">
    /// 非 Wait Running が無い無進捗の上限。<see cref="Timeout.InfiniteTimeSpan"/> 以下なら watchdog しない。
    /// </param>
    /// <param name="ct">キャンセル。</param>
    public async Task AwaitLocalExecutionLoadAsync(
        Guid executionId,
        TimeSpan noProgressTimeout,
        CancellationToken ct)
    {
        var engineId = executionId.ToString();
        var idleWatch = System.Diagnostics.Stopwatch.StartNew();
        var watchdogEnabled = noProgressTimeout > TimeSpan.Zero
            && noProgressTimeout != Timeout.InfiniteTimeSpan;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = engine.GetSnapshot(engineId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
            {
                // 終端後に Unload すると投影キューが engine 不在でスキップし、
                // Start 直後の不完全 graph のまま Completed だけ残る。最終投影を先に確定する。
                await projection.DrainAsync(executionId, ct).ConfigureAwait(false);
                await projection.UpdateProjectionFromEngineAsync(
                        executionId,
                        checkpoints.ShouldDiscardRuntimeCheckpointAsync,
                        checkpoints.DiscardOrRefreshRuntimeCheckpointAsync,
                        lifecycle.UpsertRuntimeCheckpointAsync,
                        ct)
                    .ConfigureAwait(false);
                engine.Unload(engineId);
                return;
            }

            var checkpoint = engine.ExportCheckpoint(engineId);
            var runningNonWait = HasRunningNonWaitNodes(engine.ExportExecutionGraph(engineId));
            // recovery hydrate 後など、PendingWaits があるのに Unload されていない場合がある。
            if (checkpoint is { PendingWaits.Count: > 0 } && !runningNonWait)
            {
                var waitNodeId = checkpoint.PendingWaits[0].NodeId;
                await checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineId, waitNodeId, ct)
                    .ConfigureAwait(false);
                return;
            }

            if (runningNonWait)
            {
                idleWatch.Restart();
            }
            else if (watchdogEnabled && idleWatch.Elapsed >= noProgressTimeout)
            {
                await checkpoints.PersistCheckpointKeepLoadedAsync(engineId, ct).ConfigureAwait(false);
                engine.Unload(engineId);
                logger.NoProgressWatchdogUnloaded(executionId, idleWatch.ElapsedMilliseconds);
                return;
            }

            await Task.Delay(LocalExecutionLoadPollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Wait 以外で未完了の実行中ノードがあるか。</summary>
    /// <param name="graphJson">Engine export グラフ JSON。</param>
    /// <returns>実行中の非 Wait ノードがあれば true。</returns>
    internal static bool HasRunningNonWaitNodes(string graphJson)
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
}
