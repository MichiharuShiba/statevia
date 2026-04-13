using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>Registry 用の組み込み IState 実装（no-op）。</summary>
public sealed class NoOpState : IState<Unit, Unit>
{
    public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
}

/// <summary>
/// 指定時間だけ待機してから完了する（入出力なし）。Playground や YAML 検証で「実行中」を体感させる用途。
/// </summary>
public sealed class DelayCompleteState : IState<Unit, Unit>
{
    private readonly TimeSpan _delay;

    /// <summary>待機時間を指定する。</summary>
    /// <param name="delay">0 以上。キャンセル時は <see cref="Task.Delay(TimeSpan, CancellationToken)"/> に従う。</param>
    public DelayCompleteState(TimeSpan delay) => _delay = delay;

    /// <inheritdoc />
    public async Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        await Task.Delay(_delay, ct).ConfigureAwait(false);
        return Unit.Value;
    }
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
