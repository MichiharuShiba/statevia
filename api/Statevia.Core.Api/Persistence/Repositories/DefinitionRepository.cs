using System;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

internal sealed class DefinitionRepository : IDefinitionRepository
{
    private sealed class DefinitionWithDisplay
    {
        public required WorkflowDefinitionRow Def { get; init; }
        public string? DisplayId { get; init; }
    }

    public Task<WorkflowDefinitionRow?> GetByIdAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        uow.Db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.TenantId == tenantId, ct);

    public Task AddAsync(ICoreUnitOfWork uow, WorkflowDefinitionRow row, CancellationToken ct)
    {
        uow.Db.WorkflowDefinitions.Add(row);
        return Task.CompletedTask;
    }

    public async Task<WorkflowDefinitionRow?> UpdateAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        string name,
        string sourceYaml,
        string compiledJson,
        CancellationToken ct)
    {
        var row = await uow.Db.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        row.Name = name;
        row.SourceYaml = sourceYaml;
        row.CompiledJson = compiledJson;
        row.UpdatedAt = DateTime.UtcNow;
        return row;
    }

    public async Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryDefinitionsWithDisplayIds(uow.Db, tenantId);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
            joinQuery = joinQuery.Where(x => x.Def.Name.Contains(query.NameContains));

        var sortedQuery = ApplyDefinitionsSort(joinQuery, query.Sort.SortBy, query.Sort.SortOrder);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await sortedQuery
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<(WorkflowDefinitionRow Def, string? DisplayId)> list = page.ConvertAll(x => (Def: x.Def, DisplayId: x.DisplayId));
        return (total, list);
    }

    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");
        return from def in db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == tenantId)
               join d in displayIdsForDefinition on def.DefinitionId equals d.ResourceId into dGroup
               from d in dGroup.DefaultIfEmpty()
               select new DefinitionWithDisplay { Def = def, DisplayId = d != null ? d.DisplayId : null };
    }

    private static IQueryable<DefinitionWithDisplay> ApplyDefinitionsSort(
        IQueryable<DefinitionWithDisplay> query,
        string? sortBy,
        string? sortOrder)
    {
        var normalizedSortBy = sortBy?.Trim();
        var isAsc = !string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "name" => isAsc
                ? query.OrderBy(x => x.Def.Name).ThenBy(x => x.Def.CreatedAt)
                : query.OrderByDescending(x => x.Def.Name).ThenByDescending(x => x.Def.CreatedAt),
            _ => isAsc
                ? query.OrderBy(x => x.Def.CreatedAt)
                : query.OrderByDescending(x => x.Def.CreatedAt)
        };
    }
}
