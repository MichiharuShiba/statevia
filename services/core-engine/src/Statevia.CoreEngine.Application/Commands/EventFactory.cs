using Statevia.CoreEngine.Domain.Events;

namespace Statevia.CoreEngine.Application.Commands;

/// <summary>EventEnvelope を生成する。core-events-spec §1.1 に準拠。</summary>
public static class EventFactory
{
    /// <summary>1 件のイベントを生成。eventId は新規 GUID、occurredAt は UTC ISO8601。</summary>
    public static EventEnvelope Create(
        string executionId,
        string type,
        Actor actor,
        IReadOnlyDictionary<string, object?> payload,
        string? correlationId = null,
        string? causationId = null)
    {
        return new EventEnvelope(
            Guid.NewGuid().ToString(),
            executionId,
            type,
            DateTime.UtcNow.ToString("O"),
            actor,
            EventEnvelope.SupportedSchemaVersion,
            payload,
            correlationId,
            causationId);
    }

    /// <summary>payload が空のイベントを生成。</summary>
    public static EventEnvelope Create(
        string executionId,
        string type,
        Actor actor,
        string? correlationId = null,
        string? causationId = null) =>
        Create(executionId, type, actor, new Dictionary<string, object?>(), correlationId, causationId);
}
