using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Security;
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

    private readonly IProjectAuthorizationService _projectAuth;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public DefinitionRepository(IProjectAuthorizationService projectAuth) =>
        _projectAuth = projectAuth;

    /// <inheritdoc />
    public async Task<DefinitionDetail?> GetLatestByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.Db.Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantInternalId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        var version = await uow.Db.DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    /// <inheritdoc />
    public async Task<DefinitionVersionRow?> GetVersionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionVersionId,
        CancellationToken ct)
    {
        var version = await uow.Db.DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionVersionId == definitionVersionId, ct)
            .ConfigureAwait(false);

        if (version is null)
            return null;

        var definition = await uow.Db.Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == version.DefinitionId, ct)
            .ConfigureAwait(false);

        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantInternalId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return version;
    }

    /// <inheritdoc />
    public async Task<DefinitionVersionRow?> GetVersionAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        int version,
        CancellationToken ct)
    {
        var definition = await uow.Db.Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);

        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantInternalId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return await uow.Db.DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == version,
                ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.Db.Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);

        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantInternalId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return definition.ProjectId;
    }

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
            .FirstOrDefaultAsync(x => x.DefinitionId == command.DefinitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanPublishAsync(uow, command.TenantInternalId, definition.ProjectId, ct)
            .ConfigureAwait(false);

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
        Guid tenantInternalId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryDefinitionsWithDisplayIds(uow.Db, tenantInternalId, ProjectAccessRole.Reader);

        if (!string.IsNullOrWhiteSpace(query.NameContains))
            joinQuery = joinQuery.Where(x => x.Definition.Name.Contains(query.NameContains));

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

    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(
        CoreDbContext db,
        Guid tenantInternalId,
        ProjectAccessRole minimumRole)
    {
        var accessibleProjectIds = ProjectAccessQueries.AccessibleProjectIds(db, tenantInternalId, minimumRole);
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");

        return from definition in db.Definitions.AsNoTracking()
               where accessibleProjectIds.Contains(definition.ProjectId)
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

/// <summary>project_accesses + オーナーによる project 可視性クエリ。</summary>
internal static class ProjectAccessQueries
{
    /// <summary>指定最小ロール以上でアクセス可能な project_id 集合。</summary>
    public static IQueryable<Guid> AccessibleProjectIds(
        CoreDbContext db,
        Guid tenantInternalId,
        ProjectAccessRole minimumRole)
    {
        var allowedRoles = ProjectAccessRolePolicy.RolesAtOrAbove(minimumRole);

        var owned = db.Projects
            .Where(p => p.OwnerTenantId == tenantInternalId)
            .Select(p => p.ProjectId);

        var granted = db.ProjectAccesses
            .Where(pa => pa.TenantId == tenantInternalId && allowedRoles.Contains(pa.Role))
            .Select(pa => pa.ProjectId);

        return owned.Union(granted);
    }
}
