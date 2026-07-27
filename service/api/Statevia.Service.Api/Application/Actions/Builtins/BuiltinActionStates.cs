using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>
/// Registry 用の組み込み IState 実装（no-op）。
/// 処理は行わず <see cref="Statevia.Core.Engine.Engine.ExecutionEngine"/> が渡す入力をそのまま出力として返す。
/// 条件付き edges が前段の Start <c>input</c>／直前状態の出力を <c>$.path</c> で解決できるようにする。
/// </summary>
internal sealed class NoOpState : IState<object?, object?>
{
    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) => Task.FromResult(input);
}

/// <summary>
/// 指定時間だけ待機してから完了する（入出力なし）。Playground や YAML 検証で「実行中」を体感させる用途。
/// </summary>
internal sealed class DelayCompleteState : IState<Unit, Unit>
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

/// <summary>
/// wait ブロック用。複数イベントをノードスコープで待機し、受信イベント名を出力する。
/// </summary>
/// <remarks>
/// <para>イベント名一覧は <see cref="Statevia.Service.Api.Application.Definition.ActionExecutorFactory"/> が束ねる。</para>
/// <para>出力は監査用の <c>{ "event": "&lt;eventName&gt;" }</c>。遷移先の route 解決は Engine（task 7）が eventName で行う。</para>
/// </remarks>
internal sealed class WaitOnlyState : IState<Unit, IReadOnlyDictionary<string, string>>
{
    private readonly IReadOnlyList<string> _eventNames;

    /// <summary>受付イベント名一覧で Wait 状態を構築する。</summary>
    /// <param name="eventNames">非空のイベント名。</param>
    public WaitOnlyState(IReadOnlyList<string> eventNames)
    {
        ArgumentNullException.ThrowIfNull(eventNames);
        if (eventNames.Count == 0)
        {
            throw new ArgumentException("eventNames must not be empty.", nameof(eventNames));
        }

        _eventNames = eventNames;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
    {
        var nodeId = string.IsNullOrWhiteSpace(ctx.NodeId) ? ctx.StateName : ctx.NodeId;
        var eventName = await ctx.Events.WaitForEventAsync(nodeId, _eventNames, ct).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["event"] = eventName,
        };
    }
}
