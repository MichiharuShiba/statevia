using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// イベント配送冪等テーブル <c>event_delivery_dedup</c> の永続化。
/// </summary>
internal interface IEventDeliveryDedupRepository
{
    /// <summary>主キーで行を取得する（読み取り専用）。</summary>
    Task<EventDeliveryDedupRow?> FindAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken);

    /// <summary>同一 UoW に RECEIVED 行を追加する。SaveChanges は呼び出し側。</summary>
    Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken);

    /// <summary>
    /// 主キー一致行のステータスと関連列を更新する（ExecuteUpdate。SaveChanges は不要）。
    /// </summary>
    /// <returns>更新できたとき true（0 件なら false）。</returns>
    Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid executionId,
        Guid clientEventId,
        EventDeliveryDedupStatusUpdate update,
        CancellationToken cancellationToken);
}
