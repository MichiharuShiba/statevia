using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Abstractions.Persistence;

internal interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(ICoreUnitOfWork uow, string dedupKey, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// 同一テナント・HTTP エンドポイント・冪等キーで有効な行があり、
    /// <paramref name="requestHash"/> が保存済みの <see cref="CommandDedupRow.RequestHash"/> と一致しないとき、最初の1件を返す。
    /// </summary>
    Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
        ICoreUnitOfWork uow,
        string tenantKey,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTime utcNow,
        CancellationToken ct);

    /// <summary>同一 UoW に追加のみ（SaveChanges は呼び出し側）。</summary>
    Task SaveAsync(ICoreUnitOfWork uow, CommandDedupRow row, CancellationToken ct);
}
