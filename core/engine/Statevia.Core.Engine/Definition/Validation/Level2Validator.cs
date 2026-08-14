namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// レベル 2 検証：到達可能性チェック、循環 Join 検出。
/// </summary>
public static class Level2Validator
{
    /// <summary>ワークフロー定義を検証し、エラー一覧を返します（Reachability フェーズのみ）。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ValidationPipeline.Run(definition, ValidationPhase.Reachability);
    }

    /// <summary>参照されていない状態を初期状態とする。状態が空なら null。</summary>
    /// <param name="definition">検証対象。</param>
    /// <returns>初期状態名。状態が無いとき null。</returns>
    internal static string? FindInitialState(WorkflowDefinition definition)
    {
        if (definition.States.Count == 0)
        {
            return null;
        }
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, stateDef) in definition.States)
        {
            if (stateDef.On != null)
            {
                foreach (var (_, trans) in stateDef.On)
                {
                    CollectReferencedStatesCore(trans, referenced);
                }
            }
            if (stateDef.Join?.All != null)
            {
                foreach (var s in stateDef.Join.All)
                {
                    referenced.Add(s);
                }
            }

            CollectWaitEventTargets(stateDef, referenced);
        }
        return definition.States.Keys.FirstOrDefault(s => !referenced.Contains(s)) ?? definition.States.Keys.First();
    }

    /// <summary>初期状態から到達可能な状態名を計算する。</summary>
    /// <param name="definition">検証対象。</param>
    /// <param name="start">開始状態名。</param>
    /// <returns>到達可能な状態名。</returns>
    internal static HashSet<string> ComputeReachableStates(WorkflowDefinition definition, string start)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        reachable.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!definition.States.TryGetValue(current, out var stateDef))
            {
                continue;
            }

            ProcessTransitions(stateDef, reachable, queue);
            ProcessWaitEvents(stateDef, reachable, queue);
            ProcessJoin(stateDef, current, reachable, queue);
        }

        return reachable;
    }

    private static void ProcessTransitions(StateDefinition stateDef, HashSet<string> reachable, Queue<string> queue)
    {
        if (stateDef.On == null)
        {
            return;
        }

        foreach (var (_, trans) in stateDef.On)
        {
            ProcessTransitionTreeCore(trans, reachable, queue);
        }
    }

    /// <summary>
    /// wait.events / wait.subscribe の遷移先を到達可能性グラフに追加する。
    /// </summary>
    private static void ProcessWaitEvents(StateDefinition stateDef, HashSet<string> reachable, Queue<string> queue)
    {
        if (stateDef.Wait is null)
        {
            return;
        }

        foreach (var next in stateDef.Wait.Events.Values)
        {
            EnqueueNext(string.IsNullOrWhiteSpace(next) ? null : next.Trim(), reachable, queue);
        }

        foreach (var next in stateDef.Wait.Subscribe.Select(entry => entry.Next))
        {
            EnqueueNext(string.IsNullOrWhiteSpace(next) ? null : next.Trim(), reachable, queue);
        }
    }

    /// <summary>
    /// 初期状態推定用に wait.events / wait.subscribe の遷移先を参照済み集合へ追加する。
    /// </summary>
    private static void CollectWaitEventTargets(StateDefinition stateDef, HashSet<string> referenced)
    {
        if (stateDef.Wait is null)
        {
            return;
        }

        foreach (var next in stateDef.Wait.Events.Values)
        {
            AddReferencedNext(string.IsNullOrWhiteSpace(next) ? null : next.Trim(), referenced);
        }

        foreach (var next in stateDef.Wait.Subscribe.Select(entry => entry.Next))
        {
            AddReferencedNext(string.IsNullOrWhiteSpace(next) ? null : next.Trim(), referenced);
        }
    }

    private static void CollectReferencedStatesCore(TransitionDefinition trans, HashSet<string> referenced)
    {
        AddReferencedNext(trans.Next, referenced);
        if (trans.Fork != null)
        {
            foreach (var s in trans.Fork)
            {
                referenced.Add(s);
            }
        }

        if (trans.Default is not null)
        {
            CollectReferencedStatesCore(trans.Default, referenced);
        }

        if (trans.Cases is null)
        {
            return;
        }

        foreach (var c in trans.Cases)
        {
            CollectReferencedStatesCore(c.Transition, referenced);
        }
    }

    private static void AddReferencedNext(string? next, HashSet<string> referenced)
    {
        if (next is null)
        {
            return;
        }

        referenced.Add(next);
    }

    private static void ProcessTransitionTreeCore(TransitionDefinition trans, HashSet<string> reachable, Queue<string> queue)
    {
        EnqueueNext(trans.Next, reachable, queue);
        ProcessForkTransition(trans, reachable, queue);
        if (trans.Default is not null)
        {
            ProcessTransitionTreeCore(trans.Default, reachable, queue);
        }

        if (trans.Cases is null)
        {
            return;
        }

        foreach (var c in trans.Cases)
        {
            ProcessTransitionTreeCore(c.Transition, reachable, queue);
        }
    }

    private static void EnqueueNext(string? next, HashSet<string> reachable, Queue<string> queue)
    {
        if (next != null && reachable.Add(next))
        {
            queue.Enqueue(next);
        }
    }

    private static void ProcessForkTransition(TransitionDefinition trans, HashSet<string> reachable, Queue<string> queue)
    {
        if (trans.Fork == null)
        {
            return;
        }

        foreach (var s in trans.Fork.Where(reachable.Add))
        {
            queue.Enqueue(s);
        }
    }

    private static void ProcessJoin(StateDefinition stateDef, string current, HashSet<string> reachable, Queue<string> queue)
    {
        if (stateDef.Join != null && reachable.Add(current))
        {
            queue.Enqueue(current);
        }
    }

    /// <summary>Join の all 依存を辿り循環があれば true。</summary>
    /// <param name="definition">検証対象。</param>
    /// <param name="joinState">起点の Join 状態名。</param>
    /// <param name="allOf">当該 Join の all。</param>
    /// <param name="visited">走査中の Join 名。</param>
    /// <returns>循環があれば true。</returns>
    internal static bool HasCircularJoin(WorkflowDefinition definition, string joinState, IReadOnlyList<string> allOf, HashSet<string> visited)
    {
        if (!visited.Add(joinState))
        {
            return true;
        }
        foreach (var dep in allOf)
        {
            if (!definition.States.TryGetValue(dep, out var depDef))
            {
                continue;
            }
            if (depDef.Join != null && HasCircularJoin(definition, dep, depDef.Join.All, visited))
            {
                return true;
            }
        }
        visited.Remove(joinState);
        return false;
    }
}
