using Statevia.CoreEngine.Domain.Events;

namespace Statevia.CoreEngine.Application.Commands;

/// <summary>CreateExecution コマンドを処理し、EXECUTION_CREATED を 1 件返す。core-api execution-command-handlers に準拠。</summary>
public static class CreateExecutionHandler
{
    public static IReadOnlyList<EventEnvelope> Handle(
        string executionId,
        string graphId,
        Actor actor,
        object? input,
        string? correlationId)
    {
        var payload = new Dictionary<string, object?>
        {
            ["graphId"] = graphId,
            ["input"] = input,
        };
        return new[]
        {
            EventFactory.Create(
                executionId,
                EventTypeConstants.ExecutionCreated,
                actor,
                payload,
                correlationId),
        };
    }
}
