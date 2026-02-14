namespace Statevia.Core.Abstractions;

/// <summary>
/// ワークフロー状態を表します。ユーザー定義のロジックを非同期で実行します。
/// エンジンはユーザーロジックに干渉せず、CancellationToken による協調的キャンセルのみを提供します。
/// </summary>
public interface IState<in TInput, TOutput>
{
    /// <summary>状態を非同期で実行し、結果を返します。</summary>
    Task<TOutput> ExecuteAsync(StateContext ctx, TInput input, CancellationToken ct);
}
