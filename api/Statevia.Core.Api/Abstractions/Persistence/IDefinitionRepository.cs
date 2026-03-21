using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface IDefinitionRepository
{
    Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct);
    Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct);
    Task<List<(WorkflowDefinitionRow Def, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct);

    /// <summary>一覧のページング。<paramref name="nameContains"/> は名前の部分一致（O2）。</summary>
    Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        string tenantId,
        int offset,
        int limit,
        string? nameContains,
        CancellationToken ct);
}
