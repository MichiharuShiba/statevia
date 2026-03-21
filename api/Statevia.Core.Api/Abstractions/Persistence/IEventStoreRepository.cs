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
}
