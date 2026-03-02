namespace Statevia.CoreEngine.Domain.Events;

/// <summary>
/// ドメインイベントのラッパー。core-events-spec §1.1 に準拠。
/// type は EventTypeConstants の固定一覧に限定する。
/// </summary>
/// <param name="EventId">イベント識別子（UUID）。</param>
/// <param name="ExecutionId">実行識別子。</param>
/// <param name="Type">イベント種別（EXECUTION_CREATED 等）。</param>
/// <param name="OccurredAt">発生日時（RFC3339）。</param>
/// <param name="Actor">発行主体。</param>
/// <param name="SchemaVersion">スキーマバージョン（1 固定）。</param>
/// <param name="Payload">type ごとのペイロード。core-events-spec §3 参照。</param>
/// <param name="CorrelationId">相関 ID（任意）。</param>
/// <param name="CausationId">原因イベント ID（任意）。</param>
public sealed record EventEnvelope(
    string EventId,
    string ExecutionId,
    string Type,
    string OccurredAt,
    Actor Actor,
    int SchemaVersion,
    object? Payload,
    string? CorrelationId = null,
    string? CausationId = null)
{
    /// <summary>現在サポートするスキーマバージョン。</summary>
    public const int SupportedSchemaVersion = 1;
}
