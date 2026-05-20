using System.Collections.Generic;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// workflow_id 単位で seq を採番し <c>event_store</c> に追記する。
/// </summary>
internal interface IEventStoreRepository
{
    /// <summary>同一 UoW に追記のみ（SaveChanges・トランザクションは呼び出し側）。</summary>
    Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken ct = default);

    /// <summary>
    /// 論理 event_id で重複を防ぎ、未存在のときのみ行を追加する（insert-skip）。SaveChanges は呼び出し側。
    /// </summary>
    /// <returns>新規追加したとき true。既存または同一 DbContext 内の重複のとき false。</returns>
    Task<bool> TryAppendIfAbsentByClientEventAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="workflowId"/> のイベントを <paramref name="afterSeq"/> より大きい seq のみ、昇順で最大 <paramref name="limit"/> + 1 件読む。
    /// </summary>
    Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        ICoreUnitOfWork uow,
        Guid workflowId,
        long afterSeq,
        int limit,
        CancellationToken ct = default);

    /// <summary>最大 seq。イベントが無い場合は 0。</summary>
    Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid workflowId, CancellationToken ct = default);
}
