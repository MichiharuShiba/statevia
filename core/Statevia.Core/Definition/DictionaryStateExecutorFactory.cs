using Statevia.Core.Abstractions;

namespace Statevia.Core.Definition;

/// <summary>
/// 状態名から IStateExecutor を辞書で取得するファクトリ実装。
/// </summary>
public sealed class DictionaryStateExecutorFactory : IStateExecutorFactory
{
    private readonly IReadOnlyDictionary<string, IStateExecutor> _executors;

    public DictionaryStateExecutorFactory(IReadOnlyDictionary<string, IStateExecutor> executors) => _executors = executors;

    /// <inheritdoc />
    public IStateExecutor? GetExecutor(string stateName) =>
        _executors.TryGetValue(stateName, out var exec) ? exec : null;
}
