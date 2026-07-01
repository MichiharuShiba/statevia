using System;
using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Persistence;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Persistence.Repositories;

internal sealed class ExecutionRepository : IExecutionRepository
{
    private sealed class ExecutionWithDisplay
    {
        public required ExecutionRow Execution { get; init; }
        public string? DisplayId { get; init; }
    }

    public Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct) =>
        uow.Db.Executions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId && x.TenantId == tenantId, ct);

    public Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.Db.Executions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    public async Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        ExecutionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryExecutionsWithDisplayIds(uow.Db, tenantId);

        if (!string.IsNullOrWhiteSpace(query.StatusFilter))
            joinQuery = joinQuery.Where(x => x.Execution.Status == query.StatusFilter);

        if (query.DefinitionIdFilter is not null)
            joinQuery = joinQuery.Where(x => x.Execution.DefinitionId == query.DefinitionIdFilter.Value);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
        {
            var needle = query.NameContains.Trim();
            if (Guid.TryParse(needle, out var executionGuid))
            {
                joinQuery = joinQuery.Where(
                    x => (x.DisplayId != null && x.DisplayId.Contains(needle)) || x.Execution.ExecutionId == executionGuid);
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

        List<(ExecutionRow Execution, string? DisplayId)> list = page.ConvertAll(x => (Execution: x.Execution, DisplayId: x.DisplayId));
        return (total, list);
    }

    public Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct)
    {
        uow.Db.Executions.Add(execution);
        uow.Db.ExecutionGraphSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.Db.ExecutionGraphSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    public async Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct)
    {
        var w = await uow.Db.Executions.FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct).ConfigureAwait(false);
        if (w is not null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            if (cancelRequested is not null)
            {
                w.CancelRequested = cancelRequested.Value;
            }
        }

        var g = await uow.Db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct).ConfigureAwait(false);
        if (g is not null)
        {
            g.GraphJson = graphJson;
            g.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static IQueryable<ExecutionWithDisplay> QueryExecutionsWithDisplayIds(CoreDbContext db, Guid tenantId)
    {
        var displayIdsForExecution = db.DisplayIds.Where(x => x.Kind == "execution");
        return from w in db.Executions.AsNoTracking().Where(x => x.TenantId == tenantId)
               join d in displayIdsForExecution on w.ExecutionId equals d.ResourceId into dGroup
               from d in dGroup.DefaultIfEmpty()
               select new ExecutionWithDisplay { Execution = w, DisplayId = d != null ? d.DisplayId : null };
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
                ? query.OrderBy(x => x.DisplayId).ThenBy(x => x.Execution.StartedAt)
                : query.OrderByDescending(x => x.DisplayId).ThenByDescending(x => x.Execution.StartedAt),
            _ => isAsc
                ? query.OrderBy(x => x.Execution.UpdatedAt)
                    .ThenBy(x => x.Execution.StartedAt)
                : query.OrderByDescending(x => x.Execution.UpdatedAt)
                    .ThenByDescending(x => x.Execution.StartedAt)
        };
    }
}
