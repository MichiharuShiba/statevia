using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Abstractions.Services;

namespace Statevia.Service.Api.Persistence.Repositories;

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
    public Task<DefinitionDetail?> GetLatestForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        GetLatestDetailAsync(uow, tenantId, definitionId, activeOnly: true, tracked: false, ct);

    /// <inheritdoc />
    public async Task<DefinitionRow?> GetLatestForMutationAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt == null, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanPublishAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return definition;
    }

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct) =>
        GetVersionInternalAsync(uow, tenantId, definitionId, version, activeParentOnly: false, ct);

    /// <inheritdoc />
    public async Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionVersionId,
        CancellationToken ct)
    {
        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionVersionId == definitionVersionId, ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        var definition = await uow.GetDb().Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == version.DefinitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return version;
    }

    /// <inheritdoc />
    public Task<DefinitionVersionRow?> GetVersionForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct) =>
        GetVersionInternalAsync(uow, tenantId, definitionId, version, activeParentOnly: true, ct);

    /// <inheritdoc />
    public async Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt != null, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanPublishAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return definition;
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);

        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantId, definition.ProjectId, ct)
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
        uow.GetDb().Definitions.Add(definition);
        uow.GetDb().DefinitionVersions.Add(version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(
                x => x.DefinitionId == command.DefinitionId && x.DeletedAt == null,
                ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanPublishAsync(uow, command.TenantId, definition.ProjectId, ct)
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
        uow.GetDb().DefinitionVersions.Add(versionRow);

        definition.Name = command.Name;
        definition.LatestVersion = nextVersion;
        definition.UpdatedAt = now;

        return new DefinitionDetail { Definition = definition, Version = versionRow };
    }

    /// <inheritdoc />
    public async Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        var joinQuery = QueryDefinitionsWithDisplayIds(uow.GetDb(), tenantId, ProjectAccessRole.Reader);
        if (!query.IncludeDeleted)
            joinQuery = joinQuery.Where(x => x.Definition.DeletedAt == null);

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

    /// <inheritdoc />
    public async Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        DateTime deletedAt,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return DefinitionSoftDeleteOutcome.NotFound;

        await _projectAuth
            .EnsureCanPublishAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        if (definition.DeletedAt is not null)
            return DefinitionSoftDeleteOutcome.AlreadyDeleted;

        definition.DeletedAt = deletedAt;
        definition.UpdatedAt = deletedAt;
        return DefinitionSoftDeleteOutcome.Deleted;
    }

    /// <inheritdoc />
    public async Task<DefinitionDetail?> RestoreAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await uow.GetDb().Definitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.DeletedAt != null, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanPublishAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        definition.DeletedAt = null;
        definition.UpdatedAt = now;

        // SaveChanges 前のため AsNoTracking+activeOnly 再取得は不可。追跡行と版から詳細を返す。
        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    /// <inheritdoc />
    public Task<bool> ExistsActiveSlugInProjectAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        string slug,
        Guid excludingDefinitionId,
        CancellationToken ct) =>
        uow.GetDb().Definitions.AsNoTracking()
            .AnyAsync(
                x => x.ProjectId == projectId
                     && x.Slug == slug
                     && x.DefinitionId != excludingDefinitionId
                     && x.DeletedAt == null,
                ct);

    private async Task<DefinitionDetail?> GetLatestDetailAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        bool activeOnly,
        bool tracked,
        CancellationToken ct)
    {
        var query = tracked
            ? uow.GetDb().Definitions.AsQueryable()
            : uow.GetDb().Definitions.AsNoTracking();

        if (activeOnly)
            query = WhereActive(query);

        var definition = await query
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        var version = await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == definition.LatestVersion,
                ct)
            .ConfigureAwait(false);
        if (version is null)
            return null;

        return new DefinitionDetail { Definition = definition, Version = version };
    }

    private async Task<DefinitionVersionRow?> GetVersionInternalAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        bool activeParentOnly,
        CancellationToken ct)
    {
        var definitionQuery = uow.GetDb().Definitions.AsNoTracking()
            .Where(x => x.DefinitionId == definitionId);
        if (activeParentOnly)
            definitionQuery = WhereActive(definitionQuery);

        var definition = await definitionQuery
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (definition is null)
            return null;

        await _projectAuth
            .EnsureCanReadAsync(uow, tenantId, definition.ProjectId, ct)
            .ConfigureAwait(false);

        return await uow.GetDb().DefinitionVersions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.DefinitionId == definitionId && x.Version == version,
                ct)
            .ConfigureAwait(false);
    }

    private static IQueryable<DefinitionRow> WhereActive(IQueryable<DefinitionRow> query) =>
        query.Where(x => x.DeletedAt == null);

    private static IQueryable<DefinitionWithDisplay> QueryDefinitionsWithDisplayIds(
        CoreDbContext db,
        Guid tenantId,
        ProjectAccessRole minimumRole)
    {
        var accessibleProjectIds = ProjectAccessQueries.AccessibleProjectIds(db, tenantId, minimumRole);
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
        Guid tenantId,
        ProjectAccessRole minimumRole)
    {
        var allowedRoles = ProjectAccessRolePolicy.RolesAtOrAbove(minimumRole);

        var owned = db.Projects
            .Where(p => p.OwnerTenantId == tenantId)
            .Select(p => p.ProjectId);

        var granted = db.ProjectAccesses
            .Where(pa => pa.TenantId == tenantId && allowedRoles.Contains(pa.Role))
            .Select(pa => pa.ProjectId);

        return owned.Union(granted);
    }
}
