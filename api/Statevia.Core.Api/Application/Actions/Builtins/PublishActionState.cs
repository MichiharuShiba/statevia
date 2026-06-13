using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>システムスコープのトピックへイベントを発行する Event capability（MVP stub）。</summary>
internal sealed class PublishActionState : IState<object?, object?>
{
    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("publish action requires input.topic.");
        }

        var topic = ActionInputReader.RequireString(fields, "topic");
        object? payloadSummary = fields.TryGetValue("payload", out var payloadElement)
            ? payloadElement.ValueKind.ToString()
            : null;

        ctx.Events.PublishTopic(topic, payloadSummary);

        return Task.FromResult<object?>(new Dictionary<string, object?>
        {
            ["topic"] = topic,
            ["dispatched"] = true,
        });
    }
}
