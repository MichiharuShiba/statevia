namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// execution_id 単位で seq を採番し <c>event_store</c> に追記する。
/// </summary>
public interface IEventStoreRepository
{
    Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken ct = default);

    Task<bool> TryAppendIfAbsentByClientEventAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long afterSeq,
        int limit,
        CancellationToken ct = default);

    Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct = default);
}
