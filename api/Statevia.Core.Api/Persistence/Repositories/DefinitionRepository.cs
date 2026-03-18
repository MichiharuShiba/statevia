using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

public interface IDefinitionRepository
{
    Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct);
    Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct);
    Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct);
}

public sealed class DefinitionRepository : IDefinitionRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

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
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");
        return await (
            from def in db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == tenantId)
            join d in displayIdsForDefinition on def.DefinitionId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            orderby def.CreatedAt
            select new ValueTuple<WorkflowDefinitionRow, string?>(def, d != null ? d.DisplayId : null)
        ).ToListAsync(ct).ConfigureAwait(false);
    }
}

