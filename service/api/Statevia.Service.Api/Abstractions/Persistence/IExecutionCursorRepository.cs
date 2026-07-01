using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Abstractions.Persistence;

/// <summary>execution_cursors 永続化。</summary>
internal interface IExecutionCursorRepository
{
    /// <summary>cursor を upsert する（SaveChanges は呼び出し側）。</summary>
    Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct);

    /// <summary>実行終了時などに cursor 行を削除する。</summary>
    Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>execution_id で cursor 行を取得する（読み取り専用）。</summary>
    Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);
}
