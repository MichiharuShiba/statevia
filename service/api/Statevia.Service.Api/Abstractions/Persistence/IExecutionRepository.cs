using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Abstractions.Persistence;

internal interface IExecutionRepository
{
    /// <summary>テナント ID と execution_id で execution 行を取得する（読み取り専用）。</summary>
    Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct);

    /// <summary>テナントフィルタなしで execution 行を取得する（投影キュー等の内部用途）。</summary>
    Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>
    /// 一覧のページング。<paramref name="query"/> のフィルタ・ソートを適用する。
    /// </summary>
    Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        ExecutionListPageQuery query,
        CancellationToken ct);

    /// <summary>同一 UoW にワークフロー行とスナップショットを追加する（SaveChanges は呼び出し側）。</summary>
    Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct);

    /// <summary>execution_id でスナップショット行を取得する（読み取り専用）。</summary>
    Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>同一 UoW でワークフロー行とスナップショットを更新する（SaveChanges は呼び出し側）。</summary>
    Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct);
}
