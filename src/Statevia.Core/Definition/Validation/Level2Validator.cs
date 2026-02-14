namespace Statevia.Core.Definition.Validation;

/// <summary>
/// レベル 2 検証：到達可能性チェック、循環 Join 検出。
/// </summary>
public sealed class Level2Validator
{
    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        var stateNames = new HashSet<string>(definition.States.Keys, StringComparer.OrdinalIgnoreCase);
        var initialState = FindInitialState(definition);
        if (initialState == null) { errors.Add("Could not determine initial state."); return new ValidationResult(errors); }

        var reachable = ComputeReachableStates(definition, initialState);
        var unreachable = stateNames.Except(reachable, StringComparer.OrdinalIgnoreCase).ToList();
        if (unreachable.Count > 0) errors.Add($"Unreachable states: {string.Join(", ", unreachable)}");

        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Join != null && HasCircularJoin(definition, stateName, stateDef.Join.AllOf, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                errors.Add($"Circular join detected involving: {stateName}");
        }
        return new ValidationResult(errors);
    }

    private static string? FindInitialState(WorkflowDefinition definition)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, stateDef) in definition.States)
        {
            if (stateDef.On != null)
                foreach (var (_, trans) in stateDef.On)
                {
                    if (trans.Next != null) referenced.Add(trans.Next);
                    if (trans.Fork != null) foreach (var s in trans.Fork) referenced.Add(s);
                }
            if (stateDef.Join?.AllOf != null) foreach (var s in stateDef.Join.AllOf) referenced.Add(s);
        }
        return definition.States.Keys.FirstOrDefault(s => !referenced.Contains(s)) ?? definition.States.Keys.First();
    }

    private static HashSet<string> ComputeReachableStates(WorkflowDefinition definition, string start)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        reachable.Add(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!definition.States.TryGetValue(current, out var stateDef)) continue;
            if (stateDef.On != null)
                foreach (var (_, trans) in stateDef.On)
                {
                    if (trans.Next != null && reachable.Add(trans.Next)) queue.Enqueue(trans.Next);
                    if (trans.Fork != null) foreach (var s in trans.Fork) if (reachable.Add(s)) queue.Enqueue(s);
                }
            if (stateDef.Join != null && reachable.Add(current)) queue.Enqueue(current);
        }
        return reachable;
    }

    private static bool HasCircularJoin(WorkflowDefinition definition, string joinState, IReadOnlyList<string> allOf, HashSet<string> visited)
    {
        if (!visited.Add(joinState)) return true;
        foreach (var dep in allOf)
        {
            if (!definition.States.TryGetValue(dep, out var depDef)) continue;
            if (depDef.Join != null && HasCircularJoin(definition, dep, depDef.Join.AllOf, visited)) return true;
        }
        visited.Remove(joinState);
        return false;
    }
}
