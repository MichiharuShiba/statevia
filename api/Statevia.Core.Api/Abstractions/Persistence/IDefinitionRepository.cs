using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface IDefinitionRepository
{
    Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct);
    Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct);
    Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct);
}
