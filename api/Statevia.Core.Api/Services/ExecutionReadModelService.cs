using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>
/// workflows / execution_graph_snapshots / display_ids から Execution Read Model を組み立てるサービス。
/// </summary>
internal sealed class ExecutionReadModelService : IExecutionReadModelService
{
    private readonly ICoreTransactionExecutor _executor;
    private readonly IDisplayIdService _displayIds;

    public ExecutionReadModelService(
        ICoreTransactionExecutor executor,
        IDisplayIdService displayIds)
    {
        _executor = executor;
        _displayIds = displayIds;
    }

    public async Task<ExecutionReadModel> GetByDisplayIdAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Workflow, id, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(WorkflowValidationMessages.WorkflowNotFound);

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var workflow = await uow.Db.Workflows.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.WorkflowId == uuid && x.TenantId == tenantId, innerCt)
                    .ConfigureAwait(false);
                if (workflow is null)
                    throw new NotFoundException(WorkflowValidationMessages.WorkflowNotFound);

                var snapshot = await uow.Db.ExecutionGraphSnapshots.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.WorkflowId == uuid, innerCt)
                    .ConfigureAwait(false);
                if (snapshot is null)
                    throw new NotFoundException(WorkflowValidationMessages.WorkflowNotFound);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Workflow, id, innerCt)
                    .ConfigureAwait(false) ?? workflow.WorkflowId.ToString();
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, workflow.DefinitionId.ToString(), innerCt)
                    .ConfigureAwait(false) ?? workflow.DefinitionId.ToString();

                return MapToReadModel(workflow, snapshot, displayId, graphId);
            },
            ct).ConfigureAwait(false);
    }

    private static ExecutionReadModel MapToReadModel(
        WorkflowRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        string displayId,
        string graphId)
    {
        var status = ExecutionStatusMapper.ToContractStatus(workflow.Status);

        DateTimeOffset? canceledAt = status is "CANCELED" ? workflow.UpdatedAt : null;
        DateTimeOffset? failedAt = status is "FAILED" ? workflow.UpdatedAt : null;
        DateTimeOffset? completedAt = status is "COMPLETED" ? workflow.UpdatedAt : null;

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
