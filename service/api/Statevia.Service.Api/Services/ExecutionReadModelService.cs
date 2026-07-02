using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Infrastructure;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Services;

/// <summary>
/// executions / execution_graph_snapshots / display_ids から Execution Read Model を組み立てるサービス。
/// </summary>
internal sealed class ExecutionReadModelService : IExecutionReadModelService
{
    private readonly ICoreTransactionExecutor _executor;
    private readonly IDisplayIdService _displayIds;
    private readonly ITenantContextAccessor _tenantContext;

    public ExecutionReadModelService(
        ICoreTransactionExecutor executor,
        IDisplayIdService displayIds,
        ITenantContextAccessor tenantContext)
    {
        _executor = executor;
        _displayIds = displayIds;
        _tenantContext = tenantContext;
    }

    public async Task<ExecutionReadModel> GetByDisplayIdAsync(string id, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.GetRequiredTenantId();
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, id, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var execution = await uow.GetDb().Executions.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ExecutionId == uuid && x.TenantId == tenantId, innerCt)
                    .ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var snapshot = await uow.GetDb().ExecutionGraphSnapshots.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ExecutionId == uuid, innerCt)
                    .ConfigureAwait(false);
                if (snapshot is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, id, innerCt)
                    .ConfigureAwait(false) ?? execution.ExecutionId.ToString();
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, execution.DefinitionId.ToString(), innerCt)
                    .ConfigureAwait(false) ?? execution.DefinitionId.ToString();

                return MapToReadModel(execution, snapshot, displayId, graphId);
            },
            ct).ConfigureAwait(false);
    }

    private static ExecutionReadModel MapToReadModel(
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        string displayId,
        string graphId)
    {
        var status = ExecutionStatusMapper.ToContractStatus(execution.Status);

        DateTimeOffset? canceledAt = status is "CANCELED" ? execution.UpdatedAt : null;
        DateTimeOffset? failedAt = status is "FAILED" ? execution.UpdatedAt : null;
        DateTimeOffset? completedAt = status is "COMPLETED" ? execution.UpdatedAt : null;

        var nodes = MapNodes(snapshot.GraphJson);

        return new ExecutionReadModel
        {
            ExecutionId = displayId,
            GraphId = graphId,
            Status = status,
            CancelRequestedAt = null,
            CanceledAt = canceledAt,
            FailedAt = failedAt,
            CompletedAt = completedAt,
            Nodes = nodes
        };
    }

    private static IReadOnlyList<ExecutionNodeReadModel> MapNodes(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            return Array.Empty<ExecutionNodeReadModel>();
        }

        if (!JsonDeserialize.TryDeserialize(graphJson, JsonDeserialize.CaseInsensitiveDeserializeOptions, out ExecutionGraphSnapshotDto? dto))
        {
            return Array.Empty<ExecutionNodeReadModel>();
        }

        if (dto?.Nodes is null || dto.Nodes.Count == 0)
        {
            return Array.Empty<ExecutionNodeReadModel>();
        }

        var list = new List<ExecutionNodeReadModel>(dto.Nodes.Count);
        foreach (var n in dto.Nodes)
        {
            var nodeStatus = MapNodeStatus(n);
            var canceledByExecution = string.Equals(n.Fact, "Cancelled", StringComparison.OrdinalIgnoreCase);

            list.Add(new ExecutionNodeReadModel
            {
                ExecutionNodeId = n.NodeId ?? string.Empty,
                NodeType = string.IsNullOrWhiteSpace(n.StateName) ? "Task" : n.StateName,
                Status = nodeStatus,
                Attempt = 1,
                WorkerId = null,
                WaitKey = null,
                CanceledByExecution = canceledByExecution
            });
        }

        return list;
    }

    private static string MapNodeStatus(ExecutionNodeDto node)
    {
        if (node.CompletedAt is null)
        {
            return "RUNNING";
        }

        return node.Fact switch
        {
            "Completed" => "SUCCEEDED",
            "Failed" => "FAILED",
            "Cancelled" => "CANCELED",
            "Joined" => "SUCCEEDED",
            _ => "SUCCEEDED"
        };
    }

    private sealed record ExecutionGraphSnapshotDto(List<ExecutionNodeDto>? Nodes);

    private sealed record ExecutionNodeDto(
        string? NodeId,
        string? StateName,
        DateTime? CompletedAt,
        string? Fact);
}

internal static class ExecutionStatusMapper
{
    public static string ToContractStatus(string internalStatus) =>
        internalStatus switch
        {
            "Running" => "ACTIVE",
            "Completed" => "COMPLETED",
            "Failed" => "FAILED",
            "Cancelled" => "CANCELED",
            _ => "UNKNOWN"
        };
}
