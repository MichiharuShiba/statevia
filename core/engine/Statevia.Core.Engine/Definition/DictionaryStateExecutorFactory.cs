using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// 状態名から IStateExecutor を辞書で取得するファクトリ実装。
/// </summary>
public sealed class DictionaryStateExecutorFactory : IStateExecutorFactory
{
    private readonly IReadOnlyDictionary<string, IStateExecutor> _executors;

    /// <summary>
    /// 状態名と実行器の対応を指定してファクトリを構築する。
    /// </summary>
    /// <param name="executors">状態名をキーとする実行器の辞書。</param>
    public DictionaryStateExecutorFactory(IReadOnlyDictionary<string, IStateExecutor> executors) => _executors = executors;

    /// <inheritdoc />
    public IStateExecutor? GetExecutor(string stateName) =>
        _executors.TryGetValue(stateName, out var exec) ? exec : null;
}
