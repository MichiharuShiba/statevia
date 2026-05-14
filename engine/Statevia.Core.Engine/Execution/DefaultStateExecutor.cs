using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Execution;

/// <summary>
/// IStateExecutor の既定実装。IState&lt;TIn, TOut&gt; をラップするか、
/// デリゲートで直接実行するかで状態を実行します。
/// </summary>
public sealed class DefaultStateExecutor : IStateExecutor
{
    private readonly Func<StateContext, object?, CancellationToken, Task<object?>> _execute;

    /// <summary>
    /// 状態実行をデリゲートで行う実行器を構築する。
    /// </summary>
    /// <param name="execute">コンテキスト・入力・キャンセルから出力を返す非同期処理。</param>
    public DefaultStateExecutor(Func<StateContext, object?, CancellationToken, Task<object?>> execute) => _execute = execute;

    /// <summary>
    /// <see cref="IState{TIn, TOut}"/> をラップした <see cref="DefaultStateExecutor"/> を生成する。
    /// </summary>
    /// <typeparam name="TIn">状態入力の型。</typeparam>
    /// <typeparam name="TOut">状態出力の型。</typeparam>
    /// <param name="state">実行する状態。</param>
    /// <returns>生成した実行器。</returns>
    public static DefaultStateExecutor Create<TIn, TOut>(IState<TIn, TOut> state) =>
        new(async (ctx, input, ct) =>
        {
            var typedInput = input is TIn t ? t : default!;
            return await state.ExecuteAsync(ctx, typedInput, ct).ConfigureAwait(false);
        });

    /// <inheritdoc />
    public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) => _execute(ctx, input, ct);
}
