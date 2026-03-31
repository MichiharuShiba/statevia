using System.Collections;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;

namespace Statevia.Core.Api.Application.Definition;

/// <summary>
/// nodes（UI）形式ルート → <see cref="WorkflowDefinition"/>。仕様:
/// <c>.workspace-docs/specs/in-progress/v2-nodes-to-states-conversion-spec.md</c>。
/// </summary>
public sealed class NodesWorkflowDefinitionLoader : WorkflowDefinitionLoaderBase
{
    public NodesWorkflowDefinitionLoader()
        : base()
    {
    }

    /// <inheritdoc />
    protected override WorkflowDefinition BuildDefinition(Dictionary<string, object?> root)
    {
        if (root.Count == 0)
        {
            throw new ArgumentException("Workflow definition root must be an object.");
        }

        if (root.ContainsKey("controls"))
        {
            throw new ArgumentException("Root 'controls' is not supported in MVP (see v2-nodes-to-states-conversion-spec §7).");
        }

        ValidateVersion(root);
        var workflowDict = GetChildDict(root, "workflow", StringComparer.OrdinalIgnoreCase);
        if (workflowDict.Count == 0)
        {
            throw new ArgumentException("Nodes workflow definition requires root 'workflow'.");
        }

        if (!root.TryGetValue("nodes", out var nodesRaw) || nodesRaw is not IList nodesList || nodesList.Count == 0)
        {
            throw new ArgumentException("Nodes workflow definition requires non-empty root 'nodes' array.");
        }

        var parsed = new List<ParsedNode>(nodesList.Count);
        foreach (var item in nodesList)
        {
            if (item == null)
            {
                throw new ArgumentException("nodes[] must not contain null entries.");
            }

            parsed.Add(ParsedNode.FromDict(ToStringDict(item, StringComparer.OrdinalIgnoreCase)));
        }

        ValidateStructure(parsed);
        var states = BuildStates(parsed);
        var name = ResolveWorkflowName(workflowDict);

        return new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = name },
            States = states
        };
    }

    private static void ValidateVersion(Dictionary<string, object?> root)
    {
        if (!root.TryGetValue("version", out var v) || v == null)
        {
            throw new ArgumentException("Nodes workflow definition requires root 'version: 1'.");
        }

        switch (v)
        {
            case int i when i == 1:
                return;
            case long l when l == 1:
                return;
            case string s when int.TryParse(s, out var x) && x == 1:
                return;
            default:
                throw new ArgumentException("Nodes workflow definition requires root 'version' to be integer 1.");
        }
    }

    private static string ResolveWorkflowName(Dictionary<string, object?> workflowDict)
    {
        var name = GetStr(workflowDict, "name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var id = GetStr(workflowDict, "id");
        return !string.IsNullOrWhiteSpace(id) ? id : "Unnamed";
    }

    private static void ValidateStructure(IReadOnlyList<ParsedNode> nodes)
    {
        var byId = new Dictionary<string, ParsedNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Id))
            {
                throw new ArgumentException("Every node must have a non-empty 'id'.");
            }

            if (!byId.TryAdd(n.Id, n))
            {
                throw new ArgumentException($"Duplicate node id (case-insensitive): '{n.Id}'.");
            }
        }

        var starts = nodes.Where(n => n.Kind == NodeKind.Start).ToList();
        if (starts.Count != 1)
        {
            throw new ArgumentException($"Nodes workflow requires exactly one 'type: start' node (found {starts.Count}).");
        }

        var ends = nodes.Where(n => n.Kind == NodeKind.End).ToList();
        if (ends.Count != 1)
        {
            throw new ArgumentException($"Nodes workflow requires exactly one 'type: end' node (found {ends.Count}).");
        }

        foreach (var n in nodes)
        {
            n.ValidateForbiddenMvp(byId);
            n.ValidateReferences(byId);
        }

        ValidateReachability(nodes, byId, starts[0]);
    }

    private static void ValidateReachability(
        IReadOnlyList<ParsedNode> nodes,
        Dictionary<string, ParsedNode> byId,
        ParsedNode start)
    {
        if (string.IsNullOrEmpty(start.Next))
        {
            throw new ArgumentException($"Start node '{start.Id}' must have 'next'.");
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start.Id);
        visited.Add(start.Id);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!byId.TryGetValue(id, out var n))
            {
                continue;
            }

            foreach (var t in n.OutNeighborIds())
            {
                if (visited.Add(t))
                {
                    queue.Enqueue(t);
                }
            }
        }

        foreach (var n in nodes)
        {
            if (!visited.Contains(n.Id))
            {
                throw new ArgumentException($"Unreachable node from start: '{n.Id}'.");
            }
        }
    }

    private static IReadOnlyDictionary<string, StateDefinition> BuildStates(IReadOnlyList<ParsedNode> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var n in nodes)
        {
            states[n.Id] = n.ToStateDefinition(byId, n);
        }

        return states;
    }

    /// <summary>単一 fork から各 branch が直接 join に入る MVP パターンのみ。</summary>
    private static IReadOnlyList<string> ResolveJoinAllOf(ParsedNode joinNode, IReadOnlyDictionary<string, ParsedNode> byId)
    {
        var joinId = joinNode.Id;
        var candidates = new List<(string ForkId, IReadOnlyList<string> Branches)>();

        foreach (var n in byId.Values)
        {
            if (n.Kind != NodeKind.Fork || n.Branches == null || n.Branches.Count < 2)
            {
                continue;
            }

            var ok = true;
            foreach (var b in n.Branches)
            {
                if (!byId.TryGetValue(b, out var branchHead) || branchHead.Kind == NodeKind.Join)
                {
                    ok = false;
                    break;
                }

                if (!string.Equals(branchHead.Next, joinId, StringComparison.OrdinalIgnoreCase))
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                candidates.Add((n.Id, n.Branches));
            }
        }

        if (candidates.Count == 0)
        {
            throw new ArgumentException(
                $"Join '{joinId}' has no matching fork where each branch node's 'next' points to this join. MVP supports a single fork feeding the join directly (see v2-nodes-to-states-conversion-spec §5.7).");
        }

        if (candidates.Count > 1)
        {
            var names = string.Join(", ", candidates.Select(c => c.ForkId));
            throw new ArgumentException(
                $"Join '{joinId}' matches multiple forks ({names}). MVP requires a unique fork (§5.7).");
        }

        return candidates[0].Branches;
    }

    private sealed class ParsedNode
    {
        public required string Id { get; init; }
        public required NodeKind Kind { get; init; }
        public required Dictionary<string, object?> Raw { get; init; }
        public string? Next { get; init; }
        public string? ActionId { get; init; }
        public string? WaitEvent { get; init; }
        public IReadOnlyList<string>? Branches { get; init; }
        public object? InputRaw { get; init; }

        public static ParsedNode FromDict(Dictionary<string, object?> dict)
        {
            var id = GetStr(dict, "id");
            var typeStr = GetStr(dict, "type");
            if (string.IsNullOrWhiteSpace(typeStr))
            {
                throw new ArgumentException("Every node must have 'type'.");
            }

            var kind = typeStr.Trim().ToLowerInvariant() switch
            {
                "start" => NodeKind.Start,
                "end" => NodeKind.End,
                "action" => NodeKind.Action,
                "wait" => NodeKind.Wait,
                "fork" => NodeKind.Fork,
                "join" => NodeKind.Join,
                _ => throw new ArgumentException($"Unknown node type '{typeStr}' for node '{id ?? "?"}'.")
            };

            var next = GetStr(dict, "next");
            var action = GetStr(dict, "action");
            var ev = GetStr(dict, "event");
            var branches = GetStrList(dict, "branches");
            dict.TryGetValue("input", out var inputVal);

            return new ParsedNode
            {
                Id = id ?? throw new ArgumentException("Every node must have 'id'."),
                Kind = kind,
                Raw = dict,
                Next = next,
                ActionId = action,
                WaitEvent = ev,
                Branches = branches,
                InputRaw = inputVal
            };
        }

        public IEnumerable<string> OutNeighborIds()
        {
            switch (Kind)
            {
                case NodeKind.Start:
                case NodeKind.Action:
                case NodeKind.Wait:
                case NodeKind.Join:
                    if (!string.IsNullOrEmpty(Next))
                    {
                        yield return Next;
                    }

                    break;
                case NodeKind.Fork:
                    if (Branches != null)
                    {
                        foreach (var b in Branches)
                        {
                            yield return b;
                        }
                    }

                    break;
                case NodeKind.End:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        public void ValidateForbiddenMvp(Dictionary<string, ParsedNode> byId)
        {
            switch (Kind)
            {
                case NodeKind.End:
                    if (HasKeyIgnoreCase(Raw, "next"))
                    {
                        throw new ArgumentException($"Node '{Id}': 'type: end' must not have 'next' (§3.1).");
                    }

                    break;
                case NodeKind.Action:
                    ForbidKeys(Id, Raw, "onError", "output");
                    break;
                case NodeKind.Wait:
                    ForbidKeys(Id, Raw, "timeout", "onTimeout");
                    break;
                case NodeKind.Join:
                    if (Raw.TryGetValue("mode", out var modeVal) && modeVal != null)
                    {
                        var m = modeVal.ToString()?.Trim();
                        if (!string.Equals(m, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException($"Join '{Id}': only mode 'all' is supported in MVP (found '{m}').");
                        }
                    }

                    break;
            }
        }

        public void ValidateReferences(Dictionary<string, ParsedNode> byId)
        {
            void MustExist(string? refId, string role)
            {
                if (string.IsNullOrEmpty(refId))
                {
                    return;
                }

                if (!byId.ContainsKey(refId))
                {
                    throw new ArgumentException($"Node '{Id}': {role} references unknown id '{refId}'.");
                }
            }

            switch (Kind)
            {
                case NodeKind.Start:
                    if (string.IsNullOrWhiteSpace(Next))
                    {
                        throw new ArgumentException($"Start node '{Id}' must have 'next'.");
                    }

                    MustExist(Next, "next");
                    break;
                case NodeKind.Action:
                    if (string.IsNullOrWhiteSpace(ActionId))
                    {
                        throw new ArgumentException($"Action node '{Id}' must have 'action'.");
                    }

                    if (string.IsNullOrWhiteSpace(Next))
                    {
                        throw new ArgumentException($"Action node '{Id}' must have 'next'.");
                    }

                    MustExist(Next, "next");
                    break;
                case NodeKind.Wait:
                    if (string.IsNullOrWhiteSpace(WaitEvent))
                    {
                        throw new ArgumentException($"Wait node '{Id}' must have 'event'.");
                    }

                    if (string.IsNullOrWhiteSpace(Next))
                    {
                        throw new ArgumentException($"Wait node '{Id}' must have 'next'.");
                    }

                    MustExist(Next, "next");
                    break;
                case NodeKind.Fork:
                    if (Branches == null || Branches.Count < 2)
                    {
                        throw new ArgumentException($"Fork node '{Id}' must have 'branches' with at least 2 ids.");
                    }

                    foreach (var b in Branches)
                    {
                        MustExist(b, "branches");
                    }

                    break;
                case NodeKind.Join:
                    if (string.IsNullOrWhiteSpace(Next))
                    {
                        throw new ArgumentException($"Join node '{Id}' must have 'next'.");
                    }

                    MustExist(Next, "next");
                    break;
                case NodeKind.End:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        public StateDefinition ToStateDefinition(IReadOnlyDictionary<string, ParsedNode> byId, ParsedNode self)
        {
            switch (Kind)
            {
                case NodeKind.Start:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = new TransitionDefinition { Next = Next }
                        }
                    };
                case NodeKind.End:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = new TransitionDefinition { End = true }
                        }
                    };
                case NodeKind.Action:

                    return new StateDefinition
                    {
                        Action = ActionId,
                        Input = ParseActionInput(Id, InputRaw),
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = new TransitionDefinition { Next = Next }
                        }
                    };
                case NodeKind.Wait:
                    return new StateDefinition
                    {
                        Wait = new WaitDefinition { Event = WaitEvent! },
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = new TransitionDefinition { Next = Next }
                        }
                    };
                case NodeKind.Fork:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = new TransitionDefinition { Fork = Branches!.ToList() }
                        }
                    };
                case NodeKind.Join:
                    return new StateDefinition
                    {
                        Join = new JoinDefinition { AllOf = ResolveJoinAllOf(self, byId).ToList() },
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Joined"] = new TransitionDefinition { Next = Next }
                        }
                    };
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        private static void ForbidKeys(string nodeId, Dictionary<string, object?> raw, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (HasKeyIgnoreCase(raw, k))
                {
                    throw new ArgumentException(
                        $"Node '{nodeId}': '{k}' is not supported in MVP (see v2-nodes-to-states-conversion-spec §7).");
                }
            }
        }

        private static StateInputDefinition? ParseActionInput(string nodeId, object? inputVal) =>
            ParseStrictInputMapping(inputVal, nodeId);
    }

    private enum NodeKind
    {
        Start,
        End,
        Action,
        Wait,
        Fork,
        Join
    }
}
