namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_cursors 永続化。</summary>
public interface IExecutionCursorRepository
{
    /// <summary>同一 execution の cursor を 1 行に収束させる（原子 upsert）。</summary>
    /// <param name="uow">同一 tx の UoW。呼び出し側がトランザクション中なら本書き込みもそれに参加する。</param>
    /// <param name="row">書き込む cursor。PK は <see cref="ExecutionCursorRow.ExecutionId"/>。</param>
    /// <param name="ct">キャンセル。</param>
    Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct);

    Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);
}
