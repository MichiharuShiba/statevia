using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    public WorkflowRepository(IDbContextFactory<CoreDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<WorkflowRow?> GetByIdAsync(string tenantId, Guid workflowId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == workflowId && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<(WorkflowRow Workflow, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "workflow");
        return await (
            from w in db.Workflows.AsNoTracking().Where(x => x.TenantId == tenantId)
            join d in displayIdsForWorkflow on w.WorkflowId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            orderby w.StartedAt descending
            select new ValueTuple<WorkflowRow, string?>(w, d != null ? d.DisplayId : null)
        ).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddWorkflowAndSnapshotAsync(WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Workflows.Add(workflow);
        db.ExecutionGraphSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(Guid workflowId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct)
            .ConfigureAwait(false);
    }

    public async Task UpdateWorkflowAndSnapshotAsync(Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var w = await db.Workflows.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (w is not null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            if (cancelRequested is not null)
            {
                w.CancelRequested = cancelRequested.Value;
            }
        }

        var g = await db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (g is not null)
        {
            g.GraphJson = graphJson;
            g.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

