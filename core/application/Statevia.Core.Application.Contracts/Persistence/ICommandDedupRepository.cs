namespace Statevia.Core.Application.Contracts.Persistence;

public interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(ICoreUnitOfWork uow, string dedupKey, DateTime utcNow, CancellationToken ct);

    Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
        ICoreUnitOfWork uow,
        string tenantKey,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTime utcNow,
        CancellationToken ct);

    Task SaveAsync(ICoreUnitOfWork uow, CommandDedupRow row, CancellationToken ct);
}
