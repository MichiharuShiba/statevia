using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;

namespace Statevia.Core.Api.Services;

public interface IDefinitionService
{
    Task<DefinitionResponse> CreateAsync(string tenantId, CreateDefinitionRequest request, CancellationToken ct);
    Task<List<DefinitionResponse>> ListAsync(string tenantId, CancellationToken ct);
    Task<DefinitionResponse?> GetAsync(string tenantId, string idOrUuid, CancellationToken ct);
}

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
        string compiledJson;
        (_, compiledJson) = _compiler.ValidateAndCompile(request.Name!, request.Yaml!);

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
            CreatedAt = now
        }, ct).ConfigureAwait(false);

        return new DefinitionResponse
        {
            DisplayId = displayId,
            ResourceId = id,
            Name = request.Name!,
            CreatedAt = now
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
            CreatedAt = p.Def.CreatedAt
        }).ToList();
    }

    public async Task<DefinitionResponse?> GetAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("definition", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            return null;

        var row = await _definitions.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            return null;

        var displayId = await _displayIds.GetDisplayIdAsync("definition", idOrUuid, ct).ConfigureAwait(false);
        return new DefinitionResponse
        {
            DisplayId = displayId ?? row.DefinitionId.ToString(),
            ResourceId = row.DefinitionId,
            Name = row.Name,
            CreatedAt = row.CreatedAt
        };
    }
}

