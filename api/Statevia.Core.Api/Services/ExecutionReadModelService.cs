using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>
/// workflows / execution_graph_snapshots / display_ids から Execution Read Model を組み立てるサービス。
/// </summary>
public sealed class ExecutionReadModelService : IExecutionReadModelService
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IDisplayIdService _displayIds;

    public ExecutionReadModelService(
        IDbContextFactory<CoreDbContext> dbFactory,
        IDisplayIdService displayIds)
    {
        _dbFactory = dbFactory;
        _displayIds = displayIds;
    }

    public async Task<ExecutionReadModel?> GetByDisplayIdAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid is null)
        {
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var workflow = await db.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == uuid && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (workflow is null)
        {
            return null;
        }

        var snapshot = await db.ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == uuid, ct)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", id, ct)
            .ConfigureAwait(false) ?? workflow.WorkflowId.ToString();
        var graphId = await _displayIds.GetDisplayIdAsync("definition", workflow.DefinitionId.ToString(), ct)
            .ConfigureAwait(false) ?? workflow.DefinitionId.ToString();

        return MapToReadModel(workflow, snapshot, displayId, graphId);
    }

    private static ExecutionReadModel MapToReadModel(
        WorkflowRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        string displayId,
        string graphId)
    {
        var status = ExecutionStatusMapper.ToContractStatus(workflow.Status);

        // TODO: cancelRequestedAt / canceledAt / failedAt / completedAt は reducer / event 由来の正確な時刻に差し替える。
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

        ExecutionGraphSnapshotDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ExecutionGraphSnapshotDto>(
                graphJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
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
                NodeId = n.NodeId ?? string.Empty,
                NodeType = "Task",
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

    private sealed class ExecutionGraphSnapshotDto
    {
        public List<ExecutionNodeDto>? Nodes { get; set; }
    }

    private sealed class ExecutionNodeDto
    {
        public string? NodeId { get; set; }
        public string? StateName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Fact { get; set; }
    }
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

