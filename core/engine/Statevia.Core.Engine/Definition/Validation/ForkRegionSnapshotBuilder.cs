using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 制御辺と Fork-Join ペア・枝 Body を 1 回構築する。
/// </summary>
internal static class ForkRegionSnapshotBuilder
{
    private static readonly IReadOnlyDictionary<string, TransitionDefinition> EmptyOn =
        new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase);

    /// <summary>定義からスナップショットを構築する。</summary>
    /// <param name="definition">検証対象。</param>
    /// <returns>Fork 領域スナップショット。</returns>
    public static ForkRegionSnapshot Build(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var edges = CollectEdges(definition);
        var outgoing = edges
            .GroupBy(static e => e.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => (IReadOnlyList<ControlEdge>)g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var incoming = edges
            .GroupBy(static e => e.To, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => (IReadOnlyList<ControlEdge>)g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var forks = CollectForks(definition);
        var joins = CollectJoins(definition);
        var (pairs, unpairedForks, unpairedJoins, ambiguous) = PairForkJoins(forks, joins);

        var pairsWithBodies = pairs
            .Select(p => p with { BranchBodies = ComputeBranchBodies(p, pairs, outgoing) })
            .ToArray();

        return new ForkRegionSnapshot
        {
            Edges = edges,
            Outgoing = outgoing,
            Incoming = incoming,
            Forks = forks,
            Joins = joins,
            Pairs = pairsWithBodies,
            UnpairedForks = unpairedForks,
            UnpairedJoins = unpairedJoins,
            AmbiguousJoins = ambiguous
        };
    }

    /// <summary>供給辺だけで <paramref name="from"/> から <paramref name="to"/> へ到達できるか。</summary>
    public static bool SupplyReaches(
        IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> outgoing,
        string from,
        string to)
    {
        return Reaches(outgoing, from, to, supplyOnly: true);
    }

    /// <summary><paramref name="from"/> から <paramref name="to"/> へ制御辺で到達できるか。</summary>
    public static bool Reaches(
        IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> outgoing,
        string from,
        string to,
        bool supplyOnly)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (comparer.Equals(from, to))
        {
            return true;
        }

        var visited = new HashSet<string>(comparer) { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            if (!outgoing.TryGetValue(queue.Dequeue(), out var list))
            {
                continue;
            }

            if (list.Any(edge => VisitReachable(edge, to, supplyOnly, comparer, visited, queue)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>1 本の制御辺を辿り、終点なら true。</summary>
    private static bool VisitReachable(
        ControlEdge edge,
        string to,
        bool supplyOnly,
        StringComparer comparer,
        HashSet<string> visited,
        Queue<string> queue)
    {
        if (supplyOnly && !edge.CountsAsSupply)
        {
            return false;
        }

        if (!visited.Add(edge.To))
        {
            return false;
        }

        if (comparer.Equals(edge.To, to))
        {
            return true;
        }

        queue.Enqueue(edge.To);
        return false;
    }

    private static List<ControlEdge> CollectEdges(WorkflowDefinition definition) =>
        definition.States.SelectMany(static kv => EdgesFrom(kv.Key, kv.Value)).ToList();

    private static IEnumerable<ControlEdge> EdgesFrom(string stateName, StateDefinition stateDef)
    {
        var onEdges = (stateDef.On ?? EmptyOn)
            .SelectMany(kv =>
            {
                var kind = IsFailedFact(kv.Key) ? ControlEdgeKind.Failed : ControlEdgeKind.Completed;
                return EnumerateTargets(kv.Value).Select(to => new ControlEdge(stateName, to, kind));
            });
        if (stateDef.Wait is not { } wait)
        {
            return onEdges;
        }

        var waitEdges = wait.Events.Values.Select(to => new ControlEdge(stateName, to, ControlEdgeKind.WaitEvent));
        var subscribeEdges = wait.Subscribe.Select(sub => new ControlEdge(stateName, sub.Next, ControlEdgeKind.Subscribe));
        return onEdges.Concat(waitEdges).Concat(subscribeEdges);
    }

    private static bool IsFailedFact(string fact) =>
        string.Equals(fact, Fact.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fact, Fact.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateTargets(TransitionDefinition? trans)
    {
        if (trans is null)
        {
            return [];
        }

        IEnumerable<string> next = string.IsNullOrWhiteSpace(trans.Next) ? [] : [trans.Next];
        IEnumerable<string> forks = trans.Fork?.Where(static s => !string.IsNullOrWhiteSpace(s)) ?? [];
        IEnumerable<string> cases = trans.Cases?.SelectMany(static c => EnumerateTargets(c.Transition)) ?? [];
        return next.Concat(forks).Concat(cases).Concat(EnumerateTargets(trans.Default));
    }

    private static Dictionary<string, IReadOnlyList<string>> CollectForks(WorkflowDefinition definition) =>
        definition.States
            .Select(kv => (
                kv.Key,
                Branches: kv.Value.On?.Values
                    .Select(static t => t.Fork)
                    .FirstOrDefault(static f => f is { Count: > 0 })))
            .Where(static x => x.Branches is { Count: > 0 })
            .ToDictionary(static x => x.Key, static x => x.Branches!, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, IReadOnlyList<string>> CollectJoins(WorkflowDefinition definition) =>
        definition.States
            .Where(static kv => kv.Value.Join?.All is { Count: > 0 })
            .ToDictionary(
                static kv => kv.Key,
                static kv => kv.Value.Join!.All,
                StringComparer.OrdinalIgnoreCase);

    private static (
        List<ForkJoinPair> Pairs,
        List<string> UnpairedForks,
        List<string> UnpairedJoins,
        List<(string JoinState, IReadOnlyList<string> ForkStates)> Ambiguous) PairForkJoins(
        IReadOnlyDictionary<string, IReadOnlyList<string>> forks,
        IReadOnlyDictionary<string, IReadOnlyList<string>> joins)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var pairs = new List<ForkJoinPair>();
        var unpairedForks = new List<string>();
        var joinOwners = new Dictionary<string, List<string>>(comparer);

        foreach (var (forkState, branches) in forks)
        {
            var branchSet = new HashSet<string>(branches, comparer);
            var matches = joins
                .Where(j => branchSet.SetEquals(j.Value))
                .Select(static j => j.Key)
                .ToList();
            switch (matches.Count)
            {
                case 1:
                    var joinState = matches[0];
                    pairs.Add(new ForkJoinPair(
                        forkState,
                        joinState,
                        branches,
                        new Dictionary<string, IReadOnlySet<string>>(comparer)));
                    if (!joinOwners.TryGetValue(joinState, out var owners))
                    {
                        owners = [];
                        joinOwners[joinState] = owners;
                    }

                    owners.Add(forkState);
                    break;
                default:
                    unpairedForks.Add(forkState);
                    break;
            }
        }

        var pairedJoins = new HashSet<string>(pairs.Select(static p => p.JoinState), comparer);
        var unpairedJoins = joins.Keys.Where(j => !pairedJoins.Contains(j)).ToList();
        var ambiguous = joinOwners
            .Where(static kv => kv.Value.Count > 1)
            .Select(kv => (kv.Key, (IReadOnlyList<string>)kv.Value))
            .ToList();

        return (pairs, unpairedForks, unpairedJoins, ambiguous);
    }

    private static Dictionary<string, IReadOnlySet<string>> ComputeBranchBodies(
        ForkJoinPair pair,
        IReadOnlyList<ForkJoinPair> allPairs,
        IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> outgoing)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var nestedForks = new HashSet<string>(
            allPairs
                .Where(p => !comparer.Equals(p.ForkState, pair.ForkState)
                    && SupplyReaches(outgoing, p.JoinState, pair.JoinState))
                .Select(static p => p.ForkState),
            comparer);
        var siblingHeads = new HashSet<string>(pair.Branches, comparer);
        return pair.Branches.ToDictionary(
            head => head,
            head => (IReadOnlySet<string>)WalkBody(head, pair.JoinState, siblingHeads, nestedForks, outgoing),
            comparer);
    }

    private static HashSet<string> WalkBody(
        string head,
        string joinState,
        HashSet<string> siblingHeads,
        HashSet<string> nestedForks,
        IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> outgoing)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var body = new HashSet<string>(comparer);
        var visited = new HashSet<string>(comparer) { head };
        var queue = new Queue<string>();
        queue.Enqueue(head);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (IsOutsideWalk(current, head, joinState, siblingHeads, comparer))
            {
                continue;
            }

            body.Add(current);
            if (nestedForks.Contains(current))
            {
                continue;
            }

            EnqueueBodyNeighbors(current, joinState, visited, queue, outgoing, comparer);
        }

        return body;
    }

    /// <summary>Join または兄弟枝先頭なら Body に含めない。</summary>
    private static bool IsOutsideWalk(
        string current,
        string head,
        string joinState,
        HashSet<string> siblingHeads,
        StringComparer comparer) =>
        comparer.Equals(current, joinState)
        || (siblingHeads.Contains(current) && !comparer.Equals(current, head));

    /// <summary>Join へ到達できる未訪問後続だけを Body 走査へ載せる。</summary>
    private static void EnqueueBodyNeighbors(
        string current,
        string joinState,
        HashSet<string> visited,
        Queue<string> queue,
        IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> outgoing,
        StringComparer comparer)
    {
        if (!outgoing.TryGetValue(current, out var list))
        {
            return;
        }

        foreach (var target in list.Select(static e => e.To))
        {
            if (comparer.Equals(target, joinState) || !visited.Add(target))
            {
                continue;
            }

            if (Reaches(outgoing, target, joinState, supplyOnly: false))
            {
                queue.Enqueue(target);
            }
        }
    }
}
