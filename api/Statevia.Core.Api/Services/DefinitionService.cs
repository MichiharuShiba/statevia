using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Services;

public sealed class DefinitionService : IDefinitionService
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
            throw new ApiValidationException("Definition name is required.", new[]
            {
                new { message = "Definition name is required.", field = "name" }
            });
        }
        if (string.IsNullOrWhiteSpace(request.Yaml))
        {
            throw new ApiValidationException("Definition YAML is required.", new[]
            {
                new { message = "Definition YAML is required.", field = "yaml" }
            });
        }

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name!, request.Yaml!);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException("Definition validation failed.", new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }

        var id = _idGenerator.NewGuid();
        var displayId = await _displayIds.AllocateAsync("definition", id, ct).ConfigureAwait(false);

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

    public async Task<List<DefinitionResponse>> ListAsync(string tenantId, CancellationToken ct)
    {
        var pairs = await _definitions.ListWithDisplayIdsAsync(tenantId, ct).ConfigureAwait(false);
        return pairs.Select(p => new DefinitionResponse
        {
            DisplayId = p.DisplayId ?? p.Def.DefinitionId.ToString(),
            ResourceId = p.Def.DefinitionId,
            Name = p.Def.Name,
            CreatedAt = p.Def.CreatedAt,
            UpdatedAt = p.Def.UpdatedAt
        }).ToList();
    }

    public async Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        string tenantId,
        int offset,
        int limit,
        string? nameContains,
        string? sortBy,
        string? sortOrder,
        CancellationToken ct)
    {
        var query = new DefinitionListPageQuery(
            Page: new PageQuery(offset, limit),
            Sort: new SortQuery(sortBy, sortOrder),
            NameContains: nameContains);
        var (total, pairs) = await _definitions
            .ListWithDisplayIdsPageAsync(tenantId, query, ct)
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
        var uuid = await _displayIds.ResolveAsync("definition", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Definition not found");

        var row = await _definitions.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException("Definition not found");

        var displayId = await _displayIds.GetDisplayIdAsync("definition", idOrUuid, ct).ConfigureAwait(false);
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
            throw new ApiValidationException("Definition name is required.", new[]
            {
                new { message = "Definition name is required.", field = "name" }
            });
        }
        if (string.IsNullOrWhiteSpace(request.Yaml))
        {
            throw new ApiValidationException("Definition YAML is required.", new[]
            {
                new { message = "Definition YAML is required.", field = "yaml" }
            });
        }

        var uuid = await _displayIds.ResolveAsync("definition", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Definition not found");

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name, request.Yaml);
        }
        catch (ArgumentException ex)
        {
            throw new ApiValidationException("Definition validation failed.", new[]
            {
                new { message = ex.Message, field = "yaml" }
            }, ex);
        }

        var updated = await _definitions
            .UpdateAsync(tenantId, uuid.Value, request.Name, request.Yaml, compiledJson, ct)
            .ConfigureAwait(false);
        if (!updated)
            throw new NotFoundException("Definition not found");

        var row = await _definitions.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException("Definition not found");

        var displayId = await _displayIds.GetDisplayIdAsync("definition", idOrUuid, ct).ConfigureAwait(false);
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

