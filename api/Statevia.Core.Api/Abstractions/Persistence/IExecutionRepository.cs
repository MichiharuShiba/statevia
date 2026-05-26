using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

internal interface IExecutionRepository
{
    Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, string tenantId, Guid workflowId, CancellationToken ct);

    /// <summary>
    /// 一覧のページング。<paramref name="query"/> のフィルタ・ソートを適用する。
    /// </summary>
    Task<(int TotalCount, List<(ExecutionRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        ExecutionListPageQuery query,
        CancellationToken ct);

    /// <summary>同一 UoW にワークフロー行とスナップショットを追加する（SaveChanges は呼び出し側）。</summary>
    Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct);

    Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid workflowId, CancellationToken ct);

    /// <summary>同一 UoW でワークフロー行とスナップショットを更新する（SaveChanges は呼び出し側）。</summary>
    Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct);
}
