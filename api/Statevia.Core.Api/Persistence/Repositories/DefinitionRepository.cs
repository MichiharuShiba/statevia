using System;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

internal sealed class DefinitionRepository : IDefinitionRepository
{
    private sealed class DefinitionWithDisplay
    {
        public required DefinitionRow Definition { get; init; }
        public required DefinitionVersionRow Version { get; init; }
        public string? DisplayId { get; init; }
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> GetLatestByIdAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.Db.Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var version = await uow.Db.DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionByIdAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionVersionId,
        CancellationToken ct) =>
        (from version in uow.Db.DefinitionVersions.AsNoTracking()
         join definition in uow.Db.Definitions.AsNoTracking()
             on version.DefinitionId equals definition.DefinitionId
         where version.DefinitionVersionId == definitionVersionId
               && definition.TenantId == tenantId
         select version).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct) =>
        (from versionRow in uow.Db.DefinitionVersions.AsNoTracking()
         join definition in uow.Db.Definitions.AsNoTracking()
             on versionRow.DefinitionId equals definition.DefinitionId
         where versionRow.DefinitionId == definitionId
               && versionRow.Version == version
               && definition.TenantId == tenantId
         select versionRow).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct)
    {
        uow.Db.Definitions.Add(definition);
        uow.Db.DefinitionVersions.Add(version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct)
    {
        var definition = await uow.Db.Definitions
            .FirstOrDefaultAsync(
                x => x.DefinitionId == command.DefinitionId && x.TenantId == command.TenantId,
                ct)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var nextVersion = definition.LatestVersion + 1;
        var now = DateTime.UtcNow;
        var versionRow = new DefinitionVersionRow
        {
            DefinitionVersionId = command.NewVersionId,
            DefinitionId = command.DefinitionId,
            Version = nextVersion,
            SourceYaml = command.SourceYaml,
            CompiledJson = command.CompiledJson,
            CreatedAt = now
        };
        uow.Db.DefinitionVersions.Add(versionRow);

        definition.Name = command.Name;
        definition.LatestVersion = nextVersion;
        definition.UpdatedAt = now;

        return new DefinitionDetail { Definition = definition, Version = versionRow };
    }

    /// <inheritdoc />
    public async Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryDefinitionsWithDisplayIds(uow.Db, tenantId);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
        {
            joinQuery = joinQuery.Where(x => x.Definition.Name.Contains(query.NameContains));
        }

        var sortedQuery = ApplyDefinitionsSort(joinQuery, query.Sort.SortBy, query.Sort.SortOrder);

        var total = await joinQuery.CountAsync(ct).ConfigureAwait(false);
        var page = await sortedQuery
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var list = page.ConvertAll(x => (
            Detail: new DefinitionDetail { Definition = x.Definition, Version = x.Version },
            DisplayId: x.DisplayId));
        return (total, list);
    }

    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(CoreDbContext db, string tenantId)
    {
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");
        return from definition in db.Definitions.AsNoTracking().Where(x => x.TenantId == tenantId)
               join version in db.DefinitionVersions.AsNoTracking()
                   on new { definition.DefinitionId, Version = definition.LatestVersion }
                   equals new { version.DefinitionId, version.Version }
               join display in displayIdsForDefinition on definition.DefinitionId equals display.ResourceId into displayGroup
               from display in displayGroup.DefaultIfEmpty()
               select new DefinitionWithDisplay
               {
                   Definition = definition,
                   Version = version,
                   DisplayId = display != null ? display.DisplayId : null
               };
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
                ? query.OrderBy(x => x.Definition.Name).ThenBy(x => x.Definition.CreatedAt)
                : query.OrderByDescending(x => x.Definition.Name).ThenByDescending(x => x.Definition.CreatedAt),
            _ => isAsc
                ? query.OrderBy(x => x.Definition.CreatedAt)
                : query.OrderByDescending(x => x.Definition.CreatedAt)
        };
    }
}
