using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

public interface IExecutionReadModelService
{
    Task<ExecutionReadModel?> GetByDisplayIdAsync(string id, string tenantId, CancellationToken ct = default);
}

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

        return MapToReadModel(workflow, snapshot, displayId);
    }

    private static ExecutionReadModel MapToReadModel(
        WorkflowRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        string displayId)
    {
        var status = ExecutionStatusMapper.ToContractStatus(workflow.Status);

        // TODO: cancelRequestedAt / canceledAt / failedAt / completedAt は reducer / event 由来の正確な時刻に差し替える。
        DateTimeOffset? canceledAt = status is "CANCELED" ? workflow.UpdatedAt : null;
        DateTimeOffset? failedAt = status is "FAILED" ? workflow.UpdatedAt : null;
        DateTimeOffset? completedAt = status is "COMPLETED" ? workflow.UpdatedAt : null;

        return new ExecutionReadModel
        {
            ExecutionId = displayId,
            // TODO: GraphId は definition 名または ExecutionGraph 内の識別子で埋める。
            GraphId = string.Empty,
            Status = status,
            CancelRequestedAt = null,
            CanceledAt = canceledAt,
            FailedAt = failedAt,
            CompletedAt = completedAt,
            // TODO: ExecutionGraph の JSON から nodes をマッピングする。
            Nodes = Array.Empty<ExecutionNodeReadModel>()
        };
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

