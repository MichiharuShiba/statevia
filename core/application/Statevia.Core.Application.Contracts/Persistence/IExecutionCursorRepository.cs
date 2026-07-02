namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_cursors 永続化。</summary>
public interface IExecutionCursorRepository
{
    Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct);

    Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);
}
