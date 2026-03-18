using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

public interface IDefinitionService
{
    Task<DefinitionResponse> CreateAsync(string tenantId, CreateDefinitionRequest request, CancellationToken ct);
    Task<List<DefinitionResponse>> ListAsync(string tenantId, CancellationToken ct);
    Task<DefinitionResponse?> GetAsync(string tenantId, string idOrUuid, CancellationToken ct);
}
