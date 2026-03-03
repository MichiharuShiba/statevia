using Statevia.CoreEngine.Domain.Events;
using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Application.Guards;

namespace Statevia.CoreEngine.Application.Commands;

/// <summary>CancelExecution コマンドを処理し、生成するイベントを返す。core-api execution-command-handlers に準拠。</summary>
public static class CancelExecutionHandler
{
    /// <summary>既に終端なら events は空。未要求なら EXECUTION_CANCEL_REQUESTED を 1 件返す。</summary>
    public static IReadOnlyList<EventEnvelope> Handle(ExecutionState state, Actor actor, string? reason, string? correlationId)
    {
        if (state.IsTerminal())
            return Array.Empty<EventEnvelope>();

        var events = new List<EventEnvelope>();
        if (state.CancelRequestedAt is null)
        {
            var payload = new Dictionary<string, object?> { ["reason"] = reason };
            events.Add(EventFactory.Create(
                state.ExecutionId,
                EventTypeConstants.ExecutionCancelRequested,
                actor,
                payload,
                correlationId));
        }
        return events;
    }
}
