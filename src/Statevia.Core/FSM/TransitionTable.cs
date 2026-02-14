using Statevia.Core.Abstractions;

namespace Statevia.Core.FSM;

/// <summary>
/// CompiledWorkflowDefinition の遷移テーブルをラップし、IFsm として評価する実装。
/// </summary>
public sealed class TransitionTable : IFsm
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> _transitions;

    public TransitionTable(IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> transitions)
        => _transitions = transitions;

    /// <inheritdoc />
    public TransitionResult Evaluate(string stateName, string fact)
    {
        if (!_transitions.TryGetValue(stateName, out var stateTransitions)) return TransitionResult.None;
        if (!stateTransitions.TryGetValue(fact, out var target)) return TransitionResult.None;
        if (target.End) return TransitionResult.ToEnd();
        if (target.Next != null) return TransitionResult.ToNext(target.Next);
        if (target.Fork != null && target.Fork.Count > 0) return TransitionResult.ToFork(target.Fork);
        return TransitionResult.None;
    }
}
