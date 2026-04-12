namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// <c>event_delivery_dedup.status</c> の取りうる値（設計上の状態遷移: RECEIVED → APPLIED / FAILED）。
/// </summary>
public static class EventDeliveryDedupStatuses
{
    /// <summary>受信済み（永続化直後）。</summary>
    public const string Received = "RECEIVED";

    /// <summary>ワークフロー適用まで完了。</summary>
    public const string Applied = "APPLIED";

    /// <summary>適用失敗。</summary>
    public const string Failed = "FAILED";
}
