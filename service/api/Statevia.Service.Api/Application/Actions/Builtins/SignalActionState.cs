using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>実行スコープ内シグナルを発行する Signal capability。</summary>
internal sealed class SignalActionState : IState<object?, Unit>
{
    /// <inheritdoc />
    public Task<Unit> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("signal action requires input.signal.");
        }

        var target = ActionInputReader.OptionalString(fields, "target") ?? "current";
        if (!string.Equals(target, "current", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("signal action target must be 'current' in MVP.");
        }

        var signalName = ActionInputReader.RequireString(fields, "signal");
        ctx.Events.Signal(signalName);
        return Task.FromResult(Unit.Value);
    }
}
