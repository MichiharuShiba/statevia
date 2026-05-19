using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Services;

internal sealed class DefinitionService : IDefinitionService
{
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IDefinitionRepository _definitions;
    private readonly IIdGenerator _idGenerator;

    public DefinitionService(
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IDefinitionRepository definitions,
        IIdGenerator idGenerator)
    {
        _displayIds = displayIds;
        _compiler = compiler;
        _definitions = definitions;
        _idGenerator = idGenerator;
    }

    public async Task<DefinitionResponse> CreateAsync(string tenantId, CreateDefinitionRequest request, CancellationToken ct)
    {
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

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name!, request.Yaml!);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException(DefinitionValidationMessages.ValidationFailed, new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }

        var id = _idGenerator.NewGuid();
        var displayId = await _displayIds.AllocateAsync(DisplayIdResourceTypes.Definition, id, ct).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        await _definitions.AddAsync(new WorkflowDefinitionRow
        {
            DefinitionId = id,
            TenantId = tenantId,
            Name = request.Name!,
            SourceYaml = request.Yaml!,
            CompiledJson = compiledJson,
            CreatedAt = now,
            UpdatedAt = now
        }, ct).ConfigureAwait(false);

        return new DefinitionResponse
        {
            DisplayId = displayId,
            ResourceId = id,
            Name = request.Name!,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public async Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        string tenantId,
        DefinitionListQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = query.Limit ?? throw new ArgumentException("limit is required for paged list");
        var offset = query.Offset ?? 0;
        var pageQuery = new DefinitionListPageQuery(
            Page: new PageQuery(offset, limit),
            Sort: new SortQuery(query.SortBy, query.SortOrder),
            NameContains: query.Name);
        var (total, pairs) = await _definitions
            .ListWithDisplayIdsPageAsync(tenantId, pageQuery, ct)
            .ConfigureAwait(false);
        var items = pairs.Select(p => new DefinitionResponse
        {
            DisplayId = p.DisplayId ?? p.Def.DefinitionId.ToString(),
            ResourceId = p.Def.DefinitionId,
            Name = p.Def.Name,
            CreatedAt = p.Def.CreatedAt,
            UpdatedAt = p.Def.UpdatedAt
        }).ToList();

        return new PagedResult<DefinitionResponse>
        {
            Items = items,
            TotalCount = total,
            Offset = offset,
            Limit = limit,
            HasMore = offset + items.Count < total
        };
    }

    public async Task<DefinitionResponse> GetAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var row = await _definitions.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        return new DefinitionResponse
        {
            DisplayId = displayId ?? row.DefinitionId.ToString(),
            ResourceId = row.DefinitionId,
            Name = row.Name,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            Yaml = row.SourceYaml
        };
    }

    public async Task<DefinitionResponse> UpdateAsync(string tenantId, string idOrUuid, UpdateDefinitionRequest request, CancellationToken ct)
    {
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

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name, request.Yaml);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException(DefinitionValidationMessages.ValidationFailed, new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }

        var updated = await _definitions
            .UpdateAsync(tenantId, uuid.Value, request.Name, request.Yaml, compiledJson, ct)
            .ConfigureAwait(false);
        if (!updated)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var row = await _definitions.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException(DefinitionValidationMessages.NotFound);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, idOrUuid, ct).ConfigureAwait(false);
        return new DefinitionResponse
        {
            DisplayId = displayId ?? row.DefinitionId.ToString(),
            ResourceId = row.DefinitionId,
            Name = row.Name,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            Yaml = row.SourceYaml
        };
    }
}

