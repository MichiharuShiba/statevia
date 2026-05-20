using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

internal interface IWorkflowRepository
{
    Task<WorkflowRow?> GetByIdAsync(ICoreUnitOfWork uow, string tenantId, Guid workflowId, CancellationToken ct);

    /// <summary>
    /// 一覧のページング。<paramref name="query"/> のフィルタ・ソートを適用する。
    /// </summary>
    Task<(int TotalCount, List<(WorkflowRow Workflow, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        WorkflowListPageQuery query,
        CancellationToken ct);

    /// <summary>同一 UoW にワークフロー行とスナップショットを追加する（SaveChanges は呼び出し側）。</summary>
    Task AddWorkflowAndSnapshotAsync(
        ICoreUnitOfWork uow,
        WorkflowRow workflow,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct);

    Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(ICoreUnitOfWork uow, Guid workflowId, CancellationToken ct);

    /// <summary>同一 UoW でワークフロー行とスナップショットを更新する（SaveChanges は呼び出し側）。</summary>
    Task UpdateWorkflowAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct);
}
