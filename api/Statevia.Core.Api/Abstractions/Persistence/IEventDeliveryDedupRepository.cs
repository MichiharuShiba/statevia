using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// イベント配送冪等テーブル <c>event_delivery_dedup</c> の永続化。
/// </summary>
public interface IEventDeliveryDedupRepository
{
    /// <summary>
    /// 主キーで行を取得する（読み取り専用）。
    /// </summary>
    Task<EventDeliveryDedupRow?> FindAsync(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 独立した <see cref="CoreDbContext"/> で RECEIVED 行を挿入し <see cref="CoreDbContext.SaveChangesAsync"/> する。
    /// 一意制約違反は <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>。
    /// </summary>
    Task InsertReceivedAsync(EventDeliveryDedupRow row, CancellationToken cancellationToken);

    /// <summary>
    /// 同一 <see cref="CoreDbContext"/> に RECEIVED 行を追加する。SaveChanges は呼び出し側。
    /// </summary>
    Task AddReceivedAsync(CoreDbContext db, EventDeliveryDedupRow row, CancellationToken cancellationToken);

    /// <summary>
    /// 主キー一致行のステータスと関連列を更新する。SaveChanges は呼び出し側不要（一括更新）。
    /// </summary>
    /// <returns>更新できたとき true（0 件なら false）。</returns>
    Task<bool> TryUpdateStatusAsync(
        CoreDbContext db,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        string status,
        DateTime utcNow,
        DateTime? appliedAt,
        string? errorCode,
        CancellationToken cancellationToken);
}
