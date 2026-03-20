using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Abstractions.Services;

public interface IGraphDefinitionService
{
    Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, string tenantId, CancellationToken ct = default);
}
