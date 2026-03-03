using Statevia.CoreEngine.Domain.Events;
using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Extensions;
using Statevia.CoreEngine.Application.Guards;

namespace Statevia.CoreEngine.Application.Commands;

/// <summary>StartExecution コマンドを処理。Guards で弾き、通過時は EXECUTION_STARTED を 1 件返す。</summary>
public static class StartExecutionHandler
{
    /// <summary>終端または Cancel 要求済みなら null（reject）。そうでなければ EXECUTION_STARTED 1 件。</summary>
    public static IReadOnlyList<EventEnvelope>? TryHandle(ExecutionState state, Actor actor, string? correlationId)
    {
        if (state.IsTerminal() || state.IsCancelRequested())
            return null;
        return new[]
        {
            EventFactory.Create(
                state.ExecutionId,
                EventTypeConstants.ExecutionStarted,
                actor,
                new Dictionary<string, object?>(),
                correlationId),
        };
    }
}
