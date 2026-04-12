using System.Collections.Generic;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// workflow_id 単位で seq を採番し <c>event_store</c> に追記する。
/// </summary>
public interface IEventStoreRepository
{
    /// <summary>専用 DbContext + トランザクションで追記（単体利用向け）。</summary>
    Task AppendAsync(Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default);

    /// <summary>
    /// 呼び出し側が開いた <paramref name="db"/> に追記のみ（SaveChanges・トランザクションは呼び出し側）。
    /// サービス層で複数テーブルを一括コミットする前提。
    /// </summary>
    Task AppendAsync(CoreDbContext db, Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="workflowId"/> + <paramref name="clientEventId"/> + <paramref name="eventType"/> から決まる
    /// 論理 <c>event_id</c> で重複を防ぎ、未存在のときのみ行を追加する（insert-skip）。SaveChanges は呼び出し側。
    /// </summary>
    /// <returns>新規追加したとき true。既存または同一 DbContext 内の重複のとき false。</returns>
    Task<bool> TryAppendIfAbsentByClientEventAsync(
        CoreDbContext db,
        Guid workflowId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="workflowId"/> のイベントを <paramref name="afterSeq"/> より大きい seq のみ、昇順で最大 <paramref name="limit"/> + 1 件読む（hasMore 判定用）。
    /// </summary>
    Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        Guid workflowId,
        long afterSeq,
        int limit,
        CancellationToken ct = default);

    /// <summary>最大 seq。イベントが無い場合は 0。</summary>
    Task<long> GetMaxSeqAsync(Guid workflowId, CancellationToken ct = default);
}
