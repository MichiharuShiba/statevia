using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

public interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct);
    Task SaveAsync(CommandDedupRow row, CancellationToken ct);
}

public sealed class CommandDedupRepository : ICommandDedupRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    public CommandDedupRepository(IDbContextFactory<CoreDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.CommandDedup.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DedupKey == dedupKey && x.ExpiresAt > utcNow, ct)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(CommandDedupRow row, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.CommandDedup.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

