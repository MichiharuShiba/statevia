namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// workflow_id 単位で seq を採番し <c>event_store</c> に追記する。
/// </summary>
public interface IEventStoreRepository
{
    /// <param name="workflowId">ワークフロー（実行）ID。</param>
    /// <param name="eventType">イベント種別。</param>
    /// <param name="payloadJson">JSON ペイロード（任意）。</param>
    Task AppendAsync(Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default);
}
