using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Infrastructure;

namespace Statevia.Core.Application.Services;

/// <summary>ワークフロー定義の登録・更新・一覧・取得。</summary>
/// <remarks>
/// <para>Runtime 権限に加え、project ロールは <see cref="IProjectAuthorizationService"/> で明示する。</para>
/// <para>一覧の project 可視性は Repository の DB フィルタ（Reader）に委譲する。</para>
/// </remarks>
internal sealed class DefinitionService : IDefinitionService
{
    private readonly IDisplayIdService _displayIds;
    private readonly IDisplayIdWriteService _displayIdWrites;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectRepository _projects;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IIdGenerator _idGenerator;
    private readonly ICoreTransactionExecutor _executor;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "DI による明示的コンストラクタ注入。")]
    public DefinitionService(
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IDefinitionRepository definitions,
        IProjectRepository projects,
        IProjectAuthorizationService projectAuth,
        ITenantContextAccessor tenantContext,
        IRuntimePermissionAuthorization runtimeAuth,
        IIdGenerator idGenerator,
        ICoreTransactionExecutor executor)
    {
        _displayIds = displayIds;
        _displayIdWrites = displayIds as IDisplayIdWriteService
            ?? throw new InvalidOperationException("IDisplayIdService must implement IDisplayIdWriteService.");
        _compiler = compiler;
        _definitions = definitions;
        _projects = projects;
        _projectAuth = projectAuth;
        _tenantContext = tenantContext;
        _runtimeAuth = runtimeAuth;
        _idGenerator = idGenerator;
        _executor = executor;
    }

    public async Task<DefinitionResponse> CreateAsync(CreateDefinitionRequest request, CancellationToken ct)
    {
        await EnsureDefinitionsWriteAsync(ct).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _tenantContext.GetRequiredTenantId();
        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name!, request.Yaml!, tenantId);
        }
        catch (ActionInputSchemaValidationException ex)
        {
            throw new ApiValidationException(
                DefinitionValidationMessages.ValidationFailed,
                MapActionInputValidationDetails(ex),
                ex);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException(DefinitionValidationMessages.ValidationFailed, new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }
        var id = _idGenerator.NewSequentialGuid();
        var versionId = _idGenerator.NewSequentialGuid();

        return await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var project = await _projects
                    .EnsureDefaultProjectAsync(uow, tenantId, _tenantContext.GetRequiredTenantKey(), innerCt)
                    .ConfigureAwait(false);

                var displayId = await _displayIdWrites
                    .AllocateAsync(uow, DisplayIdResourceTypes.Definition, id, innerCt)
                    .ConfigureAwait(false);

                var now = DateTime.UtcNow;
                var definition = new DefinitionRow
                {
                    DefinitionId = id,
                    TenantId = tenantId,
                    ProjectId = project.ProjectId,
                    Slug = DefinitionSlug.FromName(id, request.Name!),
                    Name = request.Name!,
                    LatestVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var version = new DefinitionVersionRow
                {
                    DefinitionVersionId = versionId,
                    DefinitionId = id,
                    Version = 1,
                    SourceYaml = request.Yaml!,
                    CompiledJson = compiledJson,
                    CreatedAt = now
                };
                await _definitions.AddWithInitialVersionAsync(uow, definition, version, innerCt).ConfigureAwait(false);

                return new DefinitionResponse
                {
                    DisplayId = displayId,
                    ResourceId = id,
                    Name = request.Name!,
                    LatestVersion = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            },
            ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        DefinitionListPageQuery query,
        CancellationToken ct)
    {
        await EnsureDefinitionsReadAsync(ct).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = _tenantContext.GetRequiredTenantId();

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var (total, pairs) = await _definitions
                    .ListWithDisplayIdsPageAsync(uow, tenantId, query, innerCt)
                    .ConfigureAwait(false);
                var items = pairs.Select(p => ToResponse(
                    p.Detail,
                    p.DisplayId,
                    includeYaml: false,
                    includeDeletedAt: query.IncludeDeleted)).ToList();

                return new PagedResult<DefinitionResponse>
                {
                    Items = items,
                    TotalCount = total,
                    Offset = query.Page.Offset,
                    Limit = query.Page.Limit,
                    HasMore = query.Page.Offset + items.Count < total
                };
            },
            ct).ConfigureAwait(false);
    }

    public async Task<DefinitionResponse> GetAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureDefinitionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var tenantId = _tenantContext.GetRequiredTenantId();
        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var detail = await _definitions
                    .GetLatestForApiAsync(uow, tenantId, uuid.Value, innerCt)
                    .ConfigureAwait(false);
                if (detail is null)
                    throw new NotFoundException(DefinitionValidationMessages.NotFound);

                await _projectAuth
                    .EnsureCanReadAsync(uow, tenantId, detail.Definition.ProjectId, innerCt)
                    .ConfigureAwait(false);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, idOrUuid, innerCt)
                    .ConfigureAwait(false);
                return ToResponse(detail, displayId, includeYaml: true);
            },
            ct).ConfigureAwait(false);
    }

    public async Task<DefinitionResponse> UpdateAsync(
        string idOrUuid,
        UpdateDefinitionRequest request,
        CancellationToken ct)
    {
        await EnsureDefinitionsWriteAsync(ct).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(request);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var tenantId = _tenantContext.GetRequiredTenantId();
        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name, request.Yaml, tenantId);
        }
        catch (ActionInputSchemaValidationException ex)
        {
            throw new ApiValidationException(
                DefinitionValidationMessages.ValidationFailed,
                MapActionInputValidationDetails(ex),
                ex);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException(DefinitionValidationMessages.ValidationFailed, new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }
        var newVersionId = _idGenerator.NewSequentialGuid();

        return await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var active = await RequireActiveDefinitionAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                await _projectAuth
                    .EnsureCanPublishAsync(uow, tenantId, active.ProjectId, innerCt)
                    .ConfigureAwait(false);

                var detail = await _definitions
                    .PublishVersionAsync(
                        uow,
                        new DefinitionVersionPublishCommand(
                            tenantId,
                            uuid.Value,
                            request.Name,
                            request.Yaml,
                            compiledJson,
                            newVersionId),
                        innerCt)
                    .ConfigureAwait(false);
                if (detail is null)
                    throw new NotFoundException(DefinitionValidationMessages.NotFound);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, idOrUuid, innerCt)
                    .ConfigureAwait(false);
                return ToResponse(detail, displayId, includeYaml: true);
            },
            ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureDefinitionsWriteAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var deletedAt = DateTime.UtcNow;

        var outcome = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await EnsureCanPublishOnDefinitionAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                return await _definitions.SoftDeleteAsync(uow, tenantId, uuid.Value, deletedAt, innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        if (outcome == DefinitionSoftDeleteOutcome.NotFound)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);
    }

    public async Task<DefinitionResponse> RestoreAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureDefinitionsWriteAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var tenantId = _tenantContext.GetRequiredTenantId();

        try
        {
            // SaveChanges は ExecuteReadCommittedAsync 内でコールバック後に走るため、
            // slug 部分 UNIQUE 違反の DbUpdateException はここで捕捉する。
            return await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    await EnsureCanPublishOnDefinitionAsync(uow, tenantId, uuid.Value, innerCt)
                        .ConfigureAwait(false);

                    var deleted = await RequireDeletedDefinitionAsync(uow, tenantId, uuid.Value, innerCt)
                        .ConfigureAwait(false);

                    if (await _definitions.ExistsActiveSlugInProjectAsync(
                            uow,
                            deleted.ProjectId,
                            deleted.Slug,
                            deleted.DefinitionId,
                            innerCt).ConfigureAwait(false))
                    {
                        throw new ApiValidationException(
                            DefinitionValidationMessages.SlugConflict,
                            new[] { new { message = DefinitionValidationMessages.SlugConflict, field = "slug" } });
                    }

                    // RestoreAsync は追跡エンティティから詳細を返す（SaveChanges 前の AsNoTracking 再取得は不可）。
                    var detail = await _definitions.RestoreAsync(uow, tenantId, uuid.Value, innerCt)
                        .ConfigureAwait(false);
                    if (detail is null)
                        throw new NotFoundException(DefinitionValidationMessages.NotFound);

                    var displayId = await _displayIds
                        .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, idOrUuid, innerCt)
                        .ConfigureAwait(false);
                    return ToResponse(detail, displayId, includeYaml: true);
                },
                ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (EventDeliveryRetryPolicy.IsUniqueConstraintViolation(ex))
        {
            throw new ApiValidationException(
                DefinitionValidationMessages.SlugConflict,
                new[] { new { message = DefinitionValidationMessages.SlugConflict, field = "slug" } },
                ex);
        }
    }

    private async Task<DefinitionRow> RequireActiveDefinitionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await _definitions.GetLatestForMutationAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        return definition;
    }

    /// <summary>definition の project に Publisher 以上を要求する。論理削除済みも含む。無ければ NotFound。</summary>
    private async Task EnsureCanPublishOnDefinitionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var projectId = await _definitions.ResolveProjectIdAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        if (projectId is null)
        {
            var deleted = await _definitions.GetDeletedCatalogEntryAsync(uow, tenantId, definitionId, ct)
                .ConfigureAwait(false);
            projectId = deleted?.ProjectId;
        }

        if (projectId is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        await _projectAuth.EnsureCanPublishAsync(uow, tenantId, projectId.Value, ct).ConfigureAwait(false);
    }

    private async Task<DefinitionRow> RequireDeletedDefinitionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var definition = await _definitions.GetDeletedCatalogEntryAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        if (definition is null)
        {
            var active = await _definitions.GetLatestForMutationAsync(uow, tenantId, definitionId, ct)
                .ConfigureAwait(false);
            if (active is not null)
                throw new StateConflictException(DefinitionValidationMessages.NotDeleted);

            throw new NotFoundException(DefinitionValidationMessages.NotFound);
        }

        return definition;
    }

    private Task EnsureDefinitionsReadAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, ct);

    private Task EnsureDefinitionsWriteAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsWrite, ct);

    private static object[] MapActionInputValidationDetails(ActionInputSchemaValidationException ex) =>
        ex.Errors
            .Select(error => (object)new
            {
                message = error.Message,
                field = "yaml",
                state = error.State,
                actionId = error.ActionId,
                jsonPath = error.JsonPath,
            })
            .ToArray();

    private static DefinitionResponse ToResponse(
        DefinitionDetail detail,
        string? displayId,
        bool includeYaml = false,
        bool includeDeletedAt = false) =>
        new()
        {
            DisplayId = displayId ?? detail.Definition.DefinitionId.ToString(),
            ResourceId = detail.Definition.DefinitionId,
            Name = detail.Definition.Name,
            LatestVersion = detail.Definition.LatestVersion,
            CreatedAt = detail.Definition.CreatedAt,
            UpdatedAt = detail.Definition.UpdatedAt,
            Yaml = includeYaml ? detail.Version.SourceYaml : null,
            DeletedAt = includeDeletedAt ? detail.Definition.DeletedAt : null
        };
}
