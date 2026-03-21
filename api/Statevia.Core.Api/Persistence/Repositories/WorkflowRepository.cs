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
        var rows = await QueryWorkflowsWithDisplayIds(db, tenantId)
            .OrderByDescending(x => x.Item1.StartedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.ConvertAll(x => (Workflow: x.Item1, DisplayId: x.Item2));
    }

    public async Task<(int TotalCount, List<(WorkflowRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        string tenantId,
        int offset,
        int limit,
        string? statusFilter,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var joinQuery = QueryWorkflowsWithDisplayIds(db, tenantId);

        if (!string.IsNullOrWhiteSpace(statusFilter))
            joinQuery = joinQuery.Where(x => x.Item1.Status == statusFilter);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await joinQuery
            .OrderByDescending(x => x.Item1.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<(WorkflowRow Workflow, string? DisplayId)> list = page.ConvertAll(x => (Workflow: x.Item1, DisplayId: x.Item2));
        return (total, list);
    }

    /// <summary>
    /// テナントのワークフロー行と <c>display_ids</c>（kind=workflow）の左外部結合。一覧・ページングで共通。
    /// </summary>
    private static IQueryable<ValueTuple<WorkflowRow, string?>> QueryWorkflowsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "workflow");
        return from w in db.Workflows.AsNoTracking().Where(x => x.TenantId == tenantId)
            join d in displayIdsForWorkflow on w.WorkflowId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            select new ValueTuple<WorkflowRow, string?>(w, d != null ? d.DisplayId : null);
    }

    public async Task AddWorkflowAndSnapshotAsync(WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        AddWorkflowAndSnapshotCore(db, workflow, snapshot);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task AddWorkflowAndSnapshotAsync(CoreDbContext db, WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct)
    {
        AddWorkflowAndSnapshotCore(db, workflow, snapshot);
        return Task.CompletedTask;
    }

    private static void AddWorkflowAndSnapshotCore(CoreDbContext db, WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot)
    {
        db.Workflows.Add(workflow);
        db.ExecutionGraphSnapshots.Add(snapshot);
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
        await UpdateWorkflowAndSnapshotCoreAsync(db, workflowId, status, cancelRequested, graphJson, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateWorkflowAndSnapshotAsync(CoreDbContext db, Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct)
    {
        await UpdateWorkflowAndSnapshotCoreAsync(db, workflowId, status, cancelRequested, graphJson, ct).ConfigureAwait(false);
    }

    private static async Task UpdateWorkflowAndSnapshotCoreAsync(
        CoreDbContext db,
        Guid workflowId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct)
    {
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
    }
}

