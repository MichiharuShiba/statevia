using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Persistence.Repositories;

internal sealed class CommandDedupRepository : ICommandDedupRepository
{
    public Task<CommandDedupRow?> FindValidAsync(
        ICoreUnitOfWork uow,
        string dedupKey,
        DateTime utcNow,
        CancellationToken ct) =>
        uow.GetDb().CommandDedup.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DedupKey == dedupKey && x.ExpiresAt > utcNow, ct);

    public Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
        ICoreUnitOfWork uow,
        string tenantKey,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTime utcNow,
        CancellationToken ct)
    {
        var tenantPrefix = $"{tenantKey}|";
        return uow.GetDb().CommandDedup.AsNoTracking()
            .Where(x =>
                x.ExpiresAt > utcNow
                && x.Endpoint == endpoint
                && x.IdempotencyKey == idempotencyKey
                && x.DedupKey.StartsWith(tenantPrefix)
                && (x.RequestHash == null || x.RequestHash != requestHash))
            .FirstOrDefaultAsync(ct);
    }

    public Task SaveAsync(ICoreUnitOfWork uow, CommandDedupRow row, CancellationToken ct)
    {
        uow.GetDb().CommandDedup.Add(row);
        return Task.CompletedTask;
    }
}
