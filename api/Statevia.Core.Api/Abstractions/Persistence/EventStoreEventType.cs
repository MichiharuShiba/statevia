namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// <c>event_store.type</c> に保存する、Core-API が追記するコマンド由来イベントの種別。
/// 値は DB 上で PascalCase 文字列として保存する（<see cref="EventStoreEventTypeExtensions.ToPersistedString"/>）。
/// </summary>
public enum EventStoreEventType
{
    /// <summary>POST /v1/workflows による実行開始。</summary>
    WorkflowStarted,

    /// <summary>POST .../cancel によるキャンセル処理完了後。</summary>
    WorkflowCancelled,

    /// <summary>POST .../events によるドメインイベント発火後。</summary>
    EventPublished
}

/// <summary><see cref="EventStoreEventType"/> と永続化文字列の対応。</summary>
public static class EventStoreEventTypeExtensions
{
    /// <summary>DB の <c>type</c> 列に書き込む値（従来のリテラルと互換）。</summary>
    public static string ToPersistedString(this EventStoreEventType value) => value.ToString();
}
