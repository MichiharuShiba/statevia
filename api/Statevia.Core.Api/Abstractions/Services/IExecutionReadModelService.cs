using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Abstractions.Services;

public interface IExecutionReadModelService
{
    Task<ExecutionReadModel?> GetByDisplayIdAsync(string id, string tenantId, CancellationToken ct = default);
}
