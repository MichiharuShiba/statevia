namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>executions / execution_graph_snapshots 永続化。</summary>
public interface IExecutionRepository
{
    Task<ExecutionRow?> GetByIdAsync(ICoreUnitOfWork uow, Guid tenantId, Guid executionId, CancellationToken ct);

    Task<ExecutionRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    Task<(int TotalCount, List<(ExecutionRow Execution, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        ExecutionListPageQuery query,
        CancellationToken ct);

    Task AddExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        ExecutionRow execution,
        ExecutionGraphSnapshotRow snapshot,
        CancellationToken ct);

    Task<ExecutionGraphSnapshotRow?> GetSnapshotByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    Task UpdateExecutionAndSnapshotAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string status,
        bool? cancelRequested,
        string graphJson,
        CancellationToken ct);

    /// <summary>載荷済み試行上限の観測印として <c>restart_lost</c> を立てる。status は変えない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    Task MarkRestartLostAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        Task.CompletedTask;
}
