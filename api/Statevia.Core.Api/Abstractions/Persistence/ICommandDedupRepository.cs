using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct);
    Task SaveAsync(CommandDedupRow row, CancellationToken ct);
    /// <summary>同一 <see cref="CoreDbContext"/> に追加のみ（SaveChanges は呼び出し側）。</summary>
    Task SaveAsync(CoreDbContext db, CommandDedupRow row, CancellationToken ct);
}
