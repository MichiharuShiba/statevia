using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行投影の更新窓口（Engine → executions / operational / queue）。
/// </summary>
/// <remarks>
/// <para><see cref="IExecutionService.UpdateProjectionFromEngineAsync"/> と operational sync・queue Drain/Enqueue を集約する。</para>
/// <para>checkpoint 寿命の破棄判定自体は呼び出し側（Facade）に残し、投影 tx 内でフックとして受け取る。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="executions">executions / snapshot 永続化。</param>
/// <param name="executionCursors">cursor 投影。</param>
/// <param name="executionWaits">wait 投影。</param>
/// <param name="idGenerator">ID 生成。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="ownership">Worker 所有トラッカー（投影後 Unload 判定）。</param>
/// <param name="projectionUpdateQueue">投影更新キュー。未注入時は no-op。</param>
/// <param name="forkChildCoordinator">子終端通知（任意）。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionProjectionOrchestrator(
    IExecutionEngine engine,
    IExecutionRepository executions,
    IExecutionCursorRepository executionCursors,
    IExecutionWaitRepository executionWaits,
    IIdGenerator idGenerator,
    ICoreTransactionExecutor executor,
    ILogger<ExecutionProjectionOrchestrator> logger,
    ExecutionOwnershipTracker ownership,
    IExecutionProjectionUpdateQueue? projectionUpdateQueue = null,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    private readonly IExecutionProjectionUpdateQueue _projectionUpdateQueue =
        projectionUpdateQueue ?? NoopExecutionProjectionUpdateQueue.Instance;

    /// <summary>投影キューを drain する。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    public Task DrainAsync(Guid executionId, CancellationToken ct) =>
        _projectionUpdateQueue.DrainAsync(executionId, ct);

    /// <summary>投影キューへ enqueue する。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    public Task EnqueueAsync(Guid executionId, CancellationToken ct) =>
        _projectionUpdateQueue.EnqueueAsync(executionId, ct);

    /// <summary>
    /// Engine スナップショットから status を写像する。
    /// </summary>
    /// <param name="snapshot">Engine スナップショット。</param>
    /// <returns>投影 status 文字列。</returns>
    public static string MapStatus(ExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.IsCompleted) return ExecutionProjectionStatuses.Completed;
        if (snapshot.IsCancelled) return ExecutionProjectionStatuses.Cancelled;
        if (snapshot.IsFailed) return ExecutionProjectionStatuses.Failed;
        return ExecutionProjectionStatuses.Running;
    }

    /// <summary>
    /// Engine から投影用の status / cancel / graph を組み立てる。欠落時は例外。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <returns>投影タプル。</returns>
    public (string Status, bool? CancelRequested, string GraphJson) BuildProjectionFromEngine(Guid executionId)
    {
        var engineId = executionId.ToString();
        var snapshot = engine.GetSnapshot(engineId);
        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Cannot project execution '{executionId}': engine snapshot is missing.");
        }

        var graphJson = engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot.IsCancelled, graphJson);
    }

    /// <summary>
    /// キュー更新向け。スナップショット欠落時は null を返しスキップする。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <returns>投影タプル。欠落時は各要素 null。</returns>
    public (string? Status, bool? CancelRequested, string? GraphJson) BuildProjectionFromEngineForQueue(Guid executionId)
    {
        var engineId = executionId.ToString();
        var snapshot = engine.GetSnapshot(engineId);
        if (snapshot is null)
        {
            logger.SkipProjectionQueueUpdateDebug(executionId);
            return (null, null, null);
        }

        var graphJson = engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot.IsCancelled, graphJson);
    }

    /// <summary>
    /// cursor / wait の operational 投影を同期する。
    /// </summary>
    /// <param name="uow">同一 tx の UoW。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="status">投影 status。</param>
    /// <param name="graphJson">グラフ JSON。</param>
    /// <param name="nodeIdToClear">クリア対象 Wait nodeId。</param>
    /// <param name="ct">キャンセル。</param>
    public Task SyncOperationalProjectionAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid tenantId,
        string status,
        string graphJson,
        string? nodeIdToClear,
        CancellationToken ct)
    {
        var snapshot = engine.GetSnapshot(executionId.ToString());
        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            tenantId,
            status,
            snapshot,
            graphJson,
            nodeIdToClear);
        return ExecutionOperationalProjectionSync.SyncAsync(
            uow,
            executionCursors,
            executionWaits,
            request,
            idGenerator,
            ct);
    }

    /// <summary>
    /// Engine 状態を executions / operational へ投影し、必要なら Unload と子終端通知を行う。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="shouldDiscardRuntimeCheckpointAsync">checkpoint 破棄候補判定。</param>
    /// <param name="discardOrRefreshRuntimeCheckpointAsync">破棄または所有中 refresh。</param>
    /// <param name="upsertRuntimeCheckpointAsync">runtime checkpoint upsert。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task UpdateProjectionFromEngineAsync(
        Guid executionId,
        Func<Guid, string, string, CancellationToken, Task<bool>> shouldDiscardRuntimeCheckpointAsync,
        Func<ICoreUnitOfWork, string, Guid, CancellationToken, Task<bool>> discardOrRefreshRuntimeCheckpointAsync,
        Func<ICoreUnitOfWork, string, Guid, CancellationToken, Task<bool>> upsertRuntimeCheckpointAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shouldDiscardRuntimeCheckpointAsync);
        ArgumentNullException.ThrowIfNull(discardOrRefreshRuntimeCheckpointAsync);
        ArgumentNullException.ThrowIfNull(upsertRuntimeCheckpointAsync);

        var (status, cancelRequested, graphJson) = BuildProjectionFromEngineForQueue(executionId);
        if (status is null || graphJson is null)
            return;

        var engineExecutionId = executionId.ToString();
        var shouldUnloadAfterProjection = await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var execution = await executions.GetByExecutionIdAsync(uow, executionId, innerCt).ConfigureAwait(false);
                if (execution is null)
                    return false;

                await executions
                    .UpdateExecutionAndSnapshotAsync(uow, executionId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        execution.TenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);

                // durable Wait / 物理 Join 待ちが無い／終端なら checkpoint を削除する。
                // ただし Worker 所有中は lease / fencing 行を消さない（Heartbeat が世代不一致で落ちる）。
                if (await shouldDiscardRuntimeCheckpointAsync(executionId, status, graphJson, innerCt)
                        .ConfigureAwait(false))
                {
                    return await discardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineExecutionId,
                            executionId,
                            innerCt)
                        .ConfigureAwait(false);
                }

                var upserted = await upsertRuntimeCheckpointAsync(uow, engineExecutionId, executionId, innerCt)
                    .ConfigureAwait(false);
                // 所有セッション中の Unload 境界は Worker（Await / EndOwnedSession）。投影からは落とさない。
                return upserted && !ownership.TryGet(executionId, out _, out _);
            },
            ct).ConfigureAwait(false);

        string? childTerminalOutputJson = null;
        string? childTerminalStatesJson = null;
        string? childTerminalVarsJson = null;
        if (ExecutionProjectionStatuses.IsTerminal(status))
        {
            (childTerminalOutputJson, childTerminalStatesJson, childTerminalVarsJson) =
                TryGetChildTerminalContextJson(engineExecutionId);
        }

        if (shouldUnloadAfterProjection)
        {
            engine.Unload(engineExecutionId);
            logger.CheckpointUnloadedAfterProjectionSync(executionId);
        }

        if (ExecutionProjectionStatuses.IsTerminal(status) && forkChildCoordinator is not null)
        {
            await forkChildCoordinator
                .NotifyChildTerminalAsync(
                    executionId,
                    status,
                    childTerminalOutputJson,
                    childTerminalStatesJson,
                    childTerminalVarsJson,
                    ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>ロード中 Engine から終端 Context（output / states / vars）を JSON 文字列として取り出す。</summary>
    private (string? OutputJson, string? StatesJson, string? VarsJson) TryGetChildTerminalContextJson(
        string engineExecutionId)
    {
        var checkpoint = engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
            return (null, null, null);

        var output = checkpoint.Context.WorkflowOutput;
        string? outputJson = null;
        if (output is not null
            && output.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            outputJson = output.Value.GetRawText();
        }

        string? statesJson = null;
        if (checkpoint.Context.States is { Count: > 0 })
        {
            statesJson = JsonSerializer.Serialize(
                checkpoint.Context.States.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value,
                    StringComparer.OrdinalIgnoreCase),
                JsonSerializerProfiles.CamelCase);
        }

        string? varsJson = null;
        var vars = checkpoint.Context.Vars;
        if (vars is not null
            && vars.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            varsJson = vars.Value.GetRawText();
        }

        return (outputJson, statesJson, varsJson);
    }

    private sealed class NoopExecutionProjectionUpdateQueue : IExecutionProjectionUpdateQueue
    {
        internal static readonly NoopExecutionProjectionUpdateQueue Instance = new();

        public Task EnqueueAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;

        public Task DrainAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
    }
}
