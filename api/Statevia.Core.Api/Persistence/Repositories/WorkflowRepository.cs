using System;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

internal sealed class WorkflowRepository : IWorkflowRepository
{
    private sealed class WorkflowWithDisplay
    {
        public required WorkflowRow Workflow { get; init; }
        public string? DisplayId { get; init; }
    }

    public Task<WorkflowRow?> GetByIdAsync(ICoreUnitOfWork uow, string tenantId, Guid workflowId, CancellationToken ct) =>
        uow.Db.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == workflowId && x.TenantId == tenantId, ct);

    public async Task<(int TotalCount, List<(WorkflowRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        WorkflowListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryWorkflowsWithDisplayIds(uow.Db, tenantId);

        if (!string.IsNullOrWhiteSpace(query.StatusFilter))
            joinQuery = joinQuery.Where(x => x.Workflow.Status == query.StatusFilter);

        if (query.DefinitionIdFilter is not null)
            joinQuery = joinQuery.Where(x => x.Workflow.DefinitionId == query.DefinitionIdFilter.Value);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
        {
            var needle = query.NameContains.Trim();
            if (Guid.TryParse(needle, out var workflowGuid))
            {
                joinQuery = joinQuery.Where(
                    x => (x.DisplayId != null && x.DisplayId.Contains(needle)) || x.Workflow.WorkflowId == workflowGuid);
            }
            else
            {
                joinQuery = joinQuery.Where(x => x.DisplayId != null && x.DisplayId.Contains(needle));
            }
        }

        var sortedQuery = ApplyWorkflowsSort(joinQuery, query.Sort.SortBy, query.Sort.SortOrder);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await sortedQuery
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<(WorkflowRow Workflow, string? DisplayId)> list = page.ConvertAll(x => (Workflow: x.Workflow, DisplayId: x.DisplayId));
        return (total, list);
    }

    public Task AddWorkflowAndSnapshotAsync(
        ICoreUnitOfWork uow,
        WorkflowRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct)
    {
        uow.Db.Workflows.Add(workflow);
        uow.Db.ExecutionGraphSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(ICoreUnitOfWork uow, Guid workflowId, CancellationToken ct) =>
        uow.Db.ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct);

    public async Task UpdateWorkflowAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct)
    {
        var w = await uow.Db.Workflows.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (w is not null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            if (cancelRequested is not null)
            {
                w.CancelRequested = cancelRequested.Value;
            }
        }

        var g = await uow.Db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (g is not null)
        {
            g.GraphJson = graphJson;
            g.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static IQueryable<WorkflowWithDisplay> QueryWorkflowsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "workflow");
        return from w in db.Workflows.AsNoTracking().Where(x => x.TenantId == tenantId)
               join d in displayIdsForWorkflow on w.WorkflowId equals d.ResourceId into dGroup
               from d in dGroup.DefaultIfEmpty()
               select new WorkflowWithDisplay { Workflow = w, DisplayId = d != null ? d.DisplayId : null };
    }

    private static IQueryable<WorkflowWithDisplay> ApplyWorkflowsSort(
        IQueryable<WorkflowWithDisplay> query,
        string? sortBy,
        string? sortOrder)
    {
        var normalizedSortBy = sortBy?.Trim();
        var isAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "displayId" => isAsc
                ? query.OrderBy(x => x.DisplayId).ThenBy(x => x.Workflow.StartedAt)
                : query.OrderByDescending(x => x.DisplayId).ThenByDescending(x => x.Workflow.StartedAt),
            _ => isAsc
                ? query.OrderBy(x => x.Workflow.UpdatedAt)
                    .ThenBy(x => x.Workflow.StartedAt)
                : query.OrderByDescending(x => x.Workflow.UpdatedAt)
                    .ThenByDescending(x => x.Workflow.StartedAt)
        };
    }
}
