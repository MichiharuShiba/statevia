using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.FSM;

/// <summary>
/// CompiledWorkflowDefinition の遷移テーブルをラップし、IFsm として評価する実装。
/// </summary>
public sealed class TransitionTable : IFsm
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> _transitions;

    /// <summary>
    /// 状態名・事実・遷移先のテーブルを指定して FSM を構築する。
    /// </summary>
    /// <param name="transitions">状態名をキーとし、事実文字列から遷移先へのマップ。</param>
    public TransitionTable(IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> transitions)
        => _transitions = transitions;

    /// <inheritdoc />
    public TransitionResult Evaluate(string stateName, string fact)
    {
        if (!_transitions.TryGetValue(stateName, out var stateTransitions))
        {
            return TransitionResult.None;
        }
        if (!stateTransitions.TryGetValue(fact, out var target))
        {
            return TransitionResult.None;
        }
        if (target.End)
        {
            return TransitionResult.ToEnd();
        }
        if (target.Next != null)
        {
            return TransitionResult.ToNext(target.Next);
        }
        if (target.Fork?.Count > 0)
        {
            return TransitionResult.ToFork(target.Fork);
        }
        return TransitionResult.None;
    }
}
