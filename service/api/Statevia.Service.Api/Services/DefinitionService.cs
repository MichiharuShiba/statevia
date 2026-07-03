using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Actions.Validation;

using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Hosting;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Services;

internal sealed class DefinitionService : IDefinitionService
{
    private readonly IDisplayIdService _displayIds;
    private readonly IDisplayIdWriteService _displayIdWrites;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectRepository _projects;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IIdGenerator _idGenerator;
    private readonly ICoreTransactionExecutor _executor;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "ASP.NET Core DI による明示的コンストラクタ注入。")]
    public DefinitionService(
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IDefinitionRepository definitions,
        IProjectRepository projects,
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
        _tenantContext = tenantContext;
        _runtimeAuth = runtimeAuth;
        _idGenerator = idGenerator;
        _executor = executor;
    }

    public async Task<DefinitionResponse> CreateAsync(CreateDefinitionRequest request, CancellationToken ct)
    {
        await EnsureDefinitionsWriteAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiValidationException(DefinitionValidationMessages.NameRequired, new[]
            {
                new { message = DefinitionValidationMessages.NameRequired, field = "name" }
            });
        }
        if (string.IsNullOrWhiteSpace(request.Yaml))
        {
            throw new ApiValidationException(DefinitionValidationMessages.YamlRequired, new[]
            {
                new { message = DefinitionValidationMessages.YamlRequired, field = "yaml" }
            });
        }

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
        var id = _idGenerator.NewGuid();
        var versionId = _idGenerator.NewGuid();

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
        DefinitionListQuery query,
        CancellationToken ct)
    {
        await EnsureDefinitionsReadAsync(ct).ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(query);
        var tenantId = _tenantContext.GetRequiredTenantId();
        var limit = query.Limit ?? throw new ArgumentException("limit is required for paged list");
        var offset = query.Offset ?? 0;
        var pageQuery = new DefinitionListPageQuery(
            Page: new PageQuery(offset, limit),
            Sort: new SortQuery(query.SortBy, query.SortOrder),
            NameContains: query.Name);

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var (total, pairs) = await _definitions
                    .ListWithDisplayIdsPageAsync(uow, tenantId, pageQuery, innerCt)
                    .ConfigureAwait(false);
                var items = pairs.Select(p => ToResponse(p.Detail, p.DisplayId)).ToList();

                return new PagedResult<DefinitionResponse>
                {
                    Items = items,
                    TotalCount = total,
                    Offset = offset,
                    Limit = limit,
                    HasMore = offset + items.Count < total
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
                var detail = await _definitions.GetLatestByIdAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                if (detail is null)
                    throw new NotFoundException(DefinitionValidationMessages.NotFound);

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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApiValidationException(DefinitionValidationMessages.NameRequired, new[]
            {
                new { message = DefinitionValidationMessages.NameRequired, field = "name" }
            });
        }
        if (string.IsNullOrWhiteSpace(request.Yaml))
        {
            throw new ApiValidationException(DefinitionValidationMessages.YamlRequired, new[]
            {
                new { message = DefinitionValidationMessages.YamlRequired, field = "yaml" }
            });
        }

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
        var newVersionId = _idGenerator.NewGuid();

        return await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
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

    private static DefinitionResponse ToResponse(DefinitionDetail detail, string? displayId, bool includeYaml = false) =>
        new()
        {
            DisplayId = displayId ?? detail.Definition.DefinitionId.ToString(),
            ResourceId = detail.Definition.DefinitionId,
            Name = detail.Definition.Name,
            LatestVersion = detail.Definition.LatestVersion,
            CreatedAt = detail.Definition.CreatedAt,
            UpdatedAt = detail.Definition.UpdatedAt,
            Yaml = includeYaml ? detail.Version.SourceYaml : null
        };
}
