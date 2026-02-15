using Statevia.Core.Abstractions;
using Statevia.Core.FSM;

namespace Statevia.Core.Join;

/// <summary>
/// IJoinTracker の実装。Join テーブルに基づき allOf の完了を追跡し、
/// 全依存状態が Completed になった Join 状態を返します。
/// </summary>
public sealed class JoinTracker : IJoinTracker
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _joinTable;
    private readonly Dictionary<string, Dictionary<string, StateResult>> _joinStateResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public JoinTracker(CompiledWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _joinTable = definition.JoinTable;
    }

    /// <inheritdoc />
    public string? RecordFact(string stateName, string fact, object? output)
    {
        lock (_lock)
        {
            foreach (var (joinState, allOf) in _joinTable)
            {
                if (!allOf.Contains(stateName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!_joinStateResults.TryGetValue(joinState, out var results))
                {
                    results = new Dictionary<string, StateResult>(StringComparer.OrdinalIgnoreCase);
                    _joinStateResults[joinState] = results;
                }
                results[stateName] = new StateResult(fact, output);
                if (fact == Fact.Failed || fact == Fact.Cancelled)
                {
                    continue;
                }
                if (results.Count == allOf.Count && results.Values.All(r => r.Fact == Fact.Completed))
                {
                    return joinState;
                }
            }
        }
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> GetJoinInputs(string joinStateName)
    {
        lock (_lock)
        {
            if (!_joinStateResults.TryGetValue(joinStateName, out var results))
            {
                return new Dictionary<string, object?>();
            }
            return results.Where(r => r.Value.Fact == Fact.Completed).ToDictionary(k => k.Key, v => v.Value.Output, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record StateResult(string Fact, object? Output);
}
