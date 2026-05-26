using System;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

internal sealed class ExecutionRepository : IExecutionRepository
{
    private sealed class ExecutionWithDisplay
    {
        public required ExecutionRow Workflow { get; init; }
        public string? DisplayId { get; init; }
    }

    public Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, string tenantId, Guid workflowId, CancellationToken ct) =>
        uow.Db.Executions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == workflowId && x.TenantId == tenantId, ct);

    public async Task<(int TotalCount, List<(ExecutionRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        ExecutionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryExecutionsWithDisplayIds(uow.Db, tenantId);

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
                    x => (x.DisplayId != null && x.DisplayId.Contains(needle)) || x.Workflow.ExecutionId == workflowGuid);
            }
            else
            {
                joinQuery = joinQuery.Where(x => x.DisplayId != null && x.DisplayId.Contains(needle));
            }
        }

        var sortedQuery = ApplyExecutionsSort(joinQuery, query.Sort.SortBy, query.Sort.SortOrder);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await sortedQuery
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<(ExecutionRow Workflow, string? DisplayId)> list = page.ConvertAll(x => (Workflow: x.Workflow, DisplayId: x.DisplayId));
        return (total, list);
    }

    public Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct)
    {
        uow.Db.Executions.Add(workflow);
        uow.Db.ExecutionGraphSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid workflowId, CancellationToken ct) =>
        uow.Db.ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == workflowId, ct);

    public async Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct)
    {
        var w = await uow.Db.Executions.FirstOrDefaultAsync(x => x.ExecutionId == workflowId, ct).ConfigureAwait(false);
        if (w is not null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            if (cancelRequested is not null)
            {
                w.CancelRequested = cancelRequested.Value;
            }
        }

        var g = await uow.Db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.ExecutionId == workflowId, ct).ConfigureAwait(false);
        if (g is not null)
        {
            g.GraphJson = graphJson;
            g.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static IQueryable<ExecutionWithDisplay> QueryExecutionsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "execution");
        return from w in db.Executions.AsNoTracking().Where(x => x.TenantId == tenantId)
               join d in displayIdsForWorkflow on w.ExecutionId equals d.ResourceId into dGroup
               from d in dGroup.DefaultIfEmpty()
               select new ExecutionWithDisplay { Workflow = w, DisplayId = d != null ? d.DisplayId : null };
    }

    private static IQueryable<ExecutionWithDisplay> ApplyExecutionsSort(
        IQueryable<ExecutionWithDisplay> query,
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
