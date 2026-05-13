using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Join;

/// <summary>
/// IJoinTracker の実装。Join テーブルと完了ポリシーに基づいて依存状態の事実を追跡し、
/// ポリシーを満たした Join 状態を返します。
/// </summary>
public sealed class JoinTracker : IJoinTracker
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _joinTable;
    private readonly Dictionary<string, Dictionary<string, JoinObservedState>> _joinStateResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _joinSourceNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IJoinCompletionPolicy> _completionPolicies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _startedJoins = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public JoinTracker(CompiledWorkflowDefinition definition, IJoinCompletionPolicyFactory? policyFactory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _joinTable = definition.JoinTable;
        var factory = policyFactory ?? new JoinCompletionPolicyFactory();
        foreach (var (joinStateName, dependencies) in _joinTable)
        {
            _completionPolicies[joinStateName] = factory.Create(joinStateName, JoinConditionKind.AllOf, dependencies);
        }
    }

    /// <inheritdoc />
    public string? RecordFact(string stateName, string fact, object? output, string? nodeId = null)
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
                    results = new Dictionary<string, JoinObservedState>(StringComparer.OrdinalIgnoreCase);
                    _joinStateResults[joinState] = results;
                }
                results[stateName] = new JoinObservedState(fact, output);
                if (!_joinSourceNodeIds.TryGetValue(joinState, out var sourceNodeIds))
                {
                    sourceNodeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _joinSourceNodeIds[joinState] = sourceNodeIds;
                }
                if (fact == Fact.Completed && !string.IsNullOrWhiteSpace(nodeId))
                {
                    sourceNodeIds[stateName] = nodeId;
                }
                else
                {
                    sourceNodeIds.Remove(stateName);
                }
                if (fact is Fact.Failed or Fact.Cancelled)
                {
                    continue;
                }
                if (_completionPolicies.TryGetValue(joinState, out var completionPolicy)
                    && completionPolicy.IsSatisfied(results))
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

    /// <inheritdoc />
    public IReadOnlyList<string> GetJoinSourceNodeIds(string joinStateName)
    {
        lock (_lock)
        {
            if (!_joinTable.TryGetValue(joinStateName, out var dependencies)
                || !_joinSourceNodeIds.TryGetValue(joinStateName, out var sourceNodeIds))
            {
                return [];
            }

            var ordered = new List<string>(dependencies.Count);
            foreach (var dependency in dependencies)
            {
                if (sourceNodeIds.TryGetValue(dependency, out var nodeId))
                {
                    ordered.Add(nodeId);
                }
            }

            return ordered;
        }
    }

    /// <inheritdoc />
    public bool TryBeginJoinExecution(string joinStateName)
    {
        lock (_lock)
        {
            if (_startedJoins.Contains(joinStateName))
            {
                return false;
            }
            if (!_joinTable.TryGetValue(joinStateName, out var dependencies) || dependencies.Count == 0)
            {
                return false;
            }
            if (!_joinStateResults.TryGetValue(joinStateName, out var results))
            {
                return false;
            }
            if (!_completionPolicies.TryGetValue(joinStateName, out var completionPolicy)
                || !completionPolicy.IsSatisfied(results))
            {
                return false;
            }

            _startedJoins.Add(joinStateName);
            return true;
        }
    }

}
