using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

public sealed class DefinitionRepository : IDefinitionRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    private sealed class DefinitionWithDisplay
    {
        public required WorkflowDefinitionRow Def { get; init; }
        public string? DisplayId { get; init; }
    }

    public DefinitionRepository(IDbContextFactory<CoreDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.WorkflowDefinitions.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await QueryDefinitionsWithDisplayIds(db, tenantId)
            .OrderBy(x => x.Def.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.ConvertAll(x => (Def: x.Def, DisplayId: x.DisplayId));
    }

    public async Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        string tenantId,
        int offset,
        int limit,
        string? nameContains,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var joinQuery = QueryDefinitionsWithDisplayIds(db, tenantId);

        if (!string.IsNullOrWhiteSpace(nameContains))
            joinQuery = joinQuery.Where(x => x.Def.Name.Contains(nameContains));

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await joinQuery
            .OrderBy(x => x.Def.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<(WorkflowDefinitionRow Def, string? DisplayId)> list = page.ConvertAll(x => (Def: x.Def, DisplayId: x.DisplayId));
        return (total, list);
    }

    /// <summary>
    /// テナントの定義行と <c>display_ids</c>（kind=definition）の左外部結合。一覧・ページングで共通。
    /// </summary>
    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");
        return from def in db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == tenantId)
            join d in displayIdsForDefinition on def.DefinitionId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            select new DefinitionWithDisplay { Def = def, DisplayId = d != null ? d.DisplayId : null };
    }
}

