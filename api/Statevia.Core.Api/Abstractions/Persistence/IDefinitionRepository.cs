using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

internal interface IDefinitionRepository
{
    Task<WorkflowDefinitionRow?> GetByIdAsync(string tenantId, Guid definitionId, CancellationToken ct);
    Task AddAsync(WorkflowDefinitionRow row, CancellationToken ct);
    Task<bool> UpdateAsync(
        string tenantId,
        Guid definitionId,
        string name,
        string sourceYaml,
        string compiledJson,
        CancellationToken ct);
    /// <summary>一覧のページング。<paramref name="query"/> の <see cref="DefinitionListPageQuery.NameContains"/> で名前部分一致。</summary>
    Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        string tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct);
}
