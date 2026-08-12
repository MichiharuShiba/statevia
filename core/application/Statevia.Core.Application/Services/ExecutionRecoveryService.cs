using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 所有喪失後の hydrate 再開（Wait 非消費）。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。Recover は Engine を hydrate し投影を同期する。</para>
/// <para>durable Wait のみ残る場合は Persist+Unload して Worker Await を解放する。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="executions">executions 永続化。</param>
/// <param name="runtimeAuth">Runtime 権限。</param>
/// <param name="mutationAuth">実行ミューテーション認可。</param>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="lifecycle">Engine hydrate / checkpoint upsert。</param>
/// <param name="waitEvents">Wait quiesce 待機。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="checkpoints">checkpoint 寿命・Persist+Unload。</param>
internal sealed class ExecutionRecoveryService(
    IExecutionEngine engine,
    IExecutionRepository executions,
    IRuntimePermissionAuthorization runtimeAuth,
    IExecutionMutationAuthorization mutationAuth,
    ITenantContextAccessor tenantContext,
    ICoreTransactionExecutor executor,
    ExecutionLifecycleCommandService lifecycle,
    ExecutionWaitEventService waitEvents,
    ExecutionProjectionOrchestrator projection,
    ExecutionCheckpointService checkpoints)
{
    /// <summary>
    /// 所有喪失後に checkpoint から Engine を hydrate し、投影を同期する（Wait は消費しない）。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="requestContext">HTTP / Worker 文脈。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">実行が見つからないとき。</exception>
    public async Task RecoverExecutionAsync(
        Guid executionId,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        if (ExecutionLifecycleCommandService.IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        // Worker が BeginOwnedSession 済み前提。hydrate 後、完了フロンティア／PendingWaits から継続する。
        await lifecycle.EnsureEngineRuntimeLoadedForMutationAsync(executionId, execution, ct).ConfigureAwait(false);

        var engineId = executionId.ToString();
        // ImportCheckpoint が起動した継続（Wait 復元または完了フロンティア遷移）が落ち着くのを待つ。
        await waitEvents.WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        var (status, cancelRequested, graphJson) = projection.BuildProjectionFromEngine(executionId);
        await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await executions
                    .UpdateExecutionAndSnapshotAsync(uow, executionId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
                await projection.SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        tenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);

                if (await checkpoints.ShouldDiscardRuntimeCheckpointAsync(executionId, status, graphJson, innerCt)
                        .ConfigureAwait(false))
                {
                    await checkpoints.DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineId,
                            executionId,
                            innerCt)
                        .ConfigureAwait(false);
                }
                else
                {
                    await lifecycle.UpsertRuntimeCheckpointAsync(uow, engineId, executionId, innerCt)
                        .ConfigureAwait(false);
                }
            },
            ct).ConfigureAwait(false);

        // Wait 復元のみで継続が無い場合、ここで Unload しないと Worker Await が解放されない。
        var restored = engine.ExportCheckpoint(engineId);
        if (restored is { PendingWaits.Count: > 0 }
            && !ExecutionOwnershipService.HasRunningNonWaitNodes(engine.ExportExecutionGraph(engineId)))
        {
            await checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineId, restored.PendingWaits[0].NodeId, ct)
                .ConfigureAwait(false);
        }
    }

    private Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    private Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }
}
