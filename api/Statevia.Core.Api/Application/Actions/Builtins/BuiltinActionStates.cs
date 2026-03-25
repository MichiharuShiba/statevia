using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>Registry 用の組み込み IState 実装（no-op）。</summary>
public sealed class NoOpState : IState<Unit, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
}

/// <summary>wait ブロック用。イベント名は状態ごとに <see cref="ActionExecutorFactory"/> が束ねる。</summary>
public sealed class WaitOnlyState : IState<Unit, Unit>
{
    private readonly string _eventName;

    public WaitOnlyState(string eventName) => _eventName = eventName;

    public async Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await ctx.Events.WaitAsync(_eventName, ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
