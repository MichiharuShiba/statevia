namespace Statevia.Core.Abstractions;

/// <summary>
/// 状態名から IStateExecutor を取得するファクトリ。
/// ワークフロー定義ごとに状態実装を登録します。
/// </summary>
public interface IStateExecutorFactory
{
    /// <summary>指定した状態名のエグゼキューターを取得します。未登録の場合は null。</summary>
    IStateExecutor? GetExecutor(string stateName);
}
