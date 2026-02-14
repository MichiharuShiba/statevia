using Statevia.Core.Abstractions;

namespace Statevia.Core.Execution;

/// <summary>
/// IStateExecutor の既定実装。IState&lt;TIn, TOut&gt; をラップするか、
/// デリゲートで直接実行するかで状態を実行します。
/// </summary>
public sealed class DefaultStateExecutor : IStateExecutor
{
    private readonly Func<StateContext, object?, CancellationToken, Task<object?>> _execute;

    public DefaultStateExecutor(Func<StateContext, object?, CancellationToken, Task<object?>> execute) => _execute = execute;

    public static DefaultStateExecutor Create<TIn, TOut>(IState<TIn, TOut> state) =>
        new(async (ctx, input, ct) =>
        {
            var typedInput = input is TIn t ? t : default!;
            return await state.ExecuteAsync(ctx, typedInput, ct);
        });

    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) => _execute(ctx, input, ct);
}
