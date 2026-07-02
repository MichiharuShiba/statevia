namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_waits 永続化。</summary>
public interface IExecutionWaitRepository
{
    Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct);

    Task DeleteByResumeTokenAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string resumeToken,
        CancellationToken ct);

    Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);
}
