namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// イベント配送冪等テーブル <c>event_delivery_dedup</c> の永続化。
/// </summary>
public interface IEventDeliveryDedupRepository
{
    Task<EventDeliveryDedupRow?> FindAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken);

    Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken);

    Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        EventDeliveryDedupStatusUpdate update,
        CancellationToken cancellationToken);
}
