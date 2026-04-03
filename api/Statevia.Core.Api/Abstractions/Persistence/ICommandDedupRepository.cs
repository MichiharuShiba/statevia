using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// 同一テナント・HTTP エンドポイント・冪等キーで有効な行があり、
    /// <paramref name="requestHash"/> が保存済みの <see cref="CommandDedupRow.RequestHash"/> と一致しないとき、最初の1件を返す。
    /// </summary>
    Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
        string tenantId,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTime utcNow,
        CancellationToken ct);
    Task SaveAsync(CommandDedupRow row, CancellationToken ct);
    /// <summary>同一 <see cref="CoreDbContext"/> に追加のみ（SaveChanges は呼び出し側）。</summary>
    Task SaveAsync(CoreDbContext db, CommandDedupRow row, CancellationToken ct);
}
