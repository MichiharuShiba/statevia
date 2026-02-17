namespace Statevia.Core.Abstractions;

/// <summary>
/// IState の実行をエンジン向けにラップするインターフェース。
/// 状態名に応じた型なしで実行できるようにします。
/// </summary>
public interface IStateExecutor
{
    /// <summary>状態を非同期で実行し、出力を object として返します。</summary>
    Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct);
}
