using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

internal interface IDefinitionRepository
{
    Task<WorkflowDefinitionRow?> GetByIdAsync(ICoreUnitOfWork uow, string tenantId, Guid definitionId, CancellationToken ct);

    /// <summary>同一 UoW に定義行を追加する（SaveChanges は呼び出し側）。</summary>
    Task AddAsync(ICoreUnitOfWork uow, WorkflowDefinitionRow row, CancellationToken ct);

    Task<WorkflowDefinitionRow?> UpdateAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        string name,
        string sourceYaml,
        string compiledJson,
        CancellationToken ct);

    /// <summary>一覧のページング。</summary>
    Task<(int TotalCount, List<(WorkflowDefinitionRow Def, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct);
}
