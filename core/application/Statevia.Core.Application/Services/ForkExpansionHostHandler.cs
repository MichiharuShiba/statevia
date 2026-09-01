using Microsoft.Extensions.Logging;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// Engine Fork 展開イベントを <see cref="IForkChildExecutionCoordinator"/> に接続する。
/// </summary>
/// <remarks>
/// <para>成功時は親を checkpoint 保存のうえ Unload し、Join 待ち中はメモリに常駐させない。</para>
/// </remarks>
public sealed class ForkExpansionHostHandler(
    ICoreTransactionExecutor executor,
    IExecutionRepository executions,
    IDefinitionRepository definitions,
    IDefinitionCompilerService compiler,
    IForkChildExecutionCoordinator coordinator,
    IExecutionService executionService,
    ILogger<ForkExpansionHostHandler> logger) : IForkExpansionHostHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(ForkExpansionEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!Guid.TryParse(evt.ExecutionId, out var parentExecutionId))
        {
            logger.ForkExpansionSkippedInvalidExecutionId(evt.ExecutionId);
            return;
        }

        var parent = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => executions.GetByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        if (parent is null)
        {
            logger.ForkExpansionSkippedParentNotFound(parentExecutionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(parent.SecuritySnapshotJson))
            throw new InvalidOperationException(
                $"Parent execution {parentExecutionId:D} has no security snapshot to inherit.");

        var versionRow = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => definitions.GetVersionForExecutionByIdAsync(
                    uow,
                    parent.TenantId,
                    parent.DefinitionVersionId,
                    innerCt),
                ct)
            .ConfigureAwait(false);
        if (versionRow is null)
            throw new InvalidOperationException(
                $"Definition version {parent.DefinitionVersionId:D} not found for parent {parentExecutionId:D}.");

        var compiled = compiler.RestoreFromStoredVersion(versionRow.SourceYaml, versionRow.CompiledJson);
        var branches = evt.Branches
            .Select(b => new ForkBranchExpansion(b.BranchState, b.MappedInput))
            .ToList();

        var request = new ForkExpansionRequest(
            ParentExecutionId: parentExecutionId,
            TenantId: parent.TenantId,
            DefinitionId: parent.DefinitionId,
            DefinitionVersionId: parent.DefinitionVersionId,
            DefinitionVersion: versionRow.Version,
            DefinitionDisplayId: parent.DefinitionId.ToString("D"),
            SecuritySnapshotJson: parent.SecuritySnapshotJson,
            CompiledDefinition: compiled,
            ForkNodeId: evt.SourceNodeId,
            Branches: branches);

        var result = await coordinator.ExpandForkAsync(request, ct).ConfigureAwait(false);
        if (!result.Succeeded)
            logger.ForkExpansionFailedParentMarkedFailed(parentExecutionId, evt.SourceNodeId);

        // 成功時も Join 待ちで Engine に常駐させない。失敗時は Coordinator が親 Failed 済み。
        await executionService
            .PersistCheckpointAndUnloadByEngineIdAsync(evt.ExecutionId, evt.SourceNodeId, ct)
            .ConfigureAwait(false);
    }
}
