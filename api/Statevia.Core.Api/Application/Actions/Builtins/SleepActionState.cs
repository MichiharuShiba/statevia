using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>指定時間待機して完了する Timing capability。</summary>
internal sealed class SleepActionState : IState<object?, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("sleep action requires input.duration.");
        }

        var delay = ActionInputReader.ParseDuration(fields);
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentException("sleep action duration must be non-negative.");
        }

        await Task.Delay(delay, ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
