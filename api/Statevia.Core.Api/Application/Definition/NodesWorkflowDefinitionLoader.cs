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

    /// <summary>
    /// nodes 形式のバージョン互換を検証する。
    /// 現在は <c>version: 1</c> のみ受理する。
    /// </summary>
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

    /// <summary>
    /// workflow.name を優先し、未指定時は workflow.id、どちらも無ければ Unnamed を使う。
    /// </summary>
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

    /// <summary>
    /// ノード集合の構造制約（ID一意性、start/end 個数、参照整合）を検証する。
    /// </summary>
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

    /// <summary>
    /// start からの到達可能性を幅優先探索で検証する。
    /// </summary>
    private static void ValidateReachability(
        IReadOnlyList<ParsedNode> nodes,
        Dictionary<string, ParsedNode> byId,
        ParsedNode start)
    {
        if (!start.OutNeighborIds().Any())
        {
            throw new ArgumentException($"Start node '{start.Id}' must have at least one outgoing transition ('next' or 'edges').");
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

    /// <summary>
    /// nodes を states へ正規化し、状態名（node.id）をキーにした辞書を構築する。
    /// </summary>
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

    /// <summary>
    /// nodes[] の単一要素を正規化した内部表現。
    /// 構文検証と states 変換の両方で使う。
    /// </summary>
    private sealed class ParsedNode
    {
        public required string Id { get; init; }
        public required NodeKind Kind { get; init; }
        public required Dictionary<string, object?> Raw { get; init; }
        public string? Next { get; init; }
        public IReadOnlyList<NodeEdgeDefinition>? Edges { get; init; }
        public string? ActionId { get; init; }
        public string? WaitEvent { get; init; }
        public IReadOnlyList<string>? Branches { get; init; }
        public object? InputRaw { get; init; }

        /// <summary>
        /// 生のノード辞書を型付き表現へ変換する。
        /// </summary>
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
            var edges = ParseEdges(id, dict);

            return new ParsedNode
            {
                Id = id ?? throw new ArgumentException("Every node must have 'id'."),
                Kind = kind,
                Raw = dict,
                Next = next,
                Edges = edges,
                ActionId = action,
                WaitEvent = ev,
                Branches = branches,
                InputRaw = inputVal
            };
        }

        /// <summary>
        /// 到達性検証に使う隣接ノード ID を列挙する。
        /// </summary>
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

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            if (!string.IsNullOrWhiteSpace(e.ToId))
                            {
                                yield return e.ToId;
                            }
                        }
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

        /// <summary>
        /// MVP で未対応のノード属性を拒否する。
        /// </summary>
        public void ValidateForbiddenMvp(Dictionary<string, ParsedNode> byId)
        {
            switch (Kind)
            {
                case NodeKind.End:
                    if (HasKeyIgnoreCase(Raw, "next"))
                    {
                        throw new ArgumentException($"Node '{Id}': 'type: end' must not have 'next' (§3.1).");
                    }

                    if (Edges is { Count: > 0 })
                    {
                        throw new ArgumentException($"Node '{Id}': 'type: end' must not have 'edges' (§3.1).");
                    }

                    break;
                case NodeKind.Action:
                    ForbidKeys(Id, Raw, "onError", "output");
                    break;
                case NodeKind.Fork:
                    if (Edges is { Count: > 0 })
                    {
                        throw new ArgumentException($"Fork node '{Id}': 'edges' is not supported in MVP.");
                    }

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

        /// <summary>
        /// ノード種別ごとの必須属性と参照先 ID の存在を検証する。
        /// </summary>
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
                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Start node '{Id}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, "next");
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToId, "edges.to");
                        }
                    }

                    break;
                case NodeKind.Action:
                    if (string.IsNullOrWhiteSpace(ActionId))
                    {
                        throw new ArgumentException($"Action node '{Id}' must have 'action'.");
                    }

                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Action node '{Id}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, "next");
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToId, "edges.to");
                        }
                    }

                    break;
                case NodeKind.Wait:
                    if (string.IsNullOrWhiteSpace(WaitEvent))
                    {
                        throw new ArgumentException($"Wait node '{Id}' must have 'event'.");
                    }

                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Wait node '{Id}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, "next");
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToId, "edges.to");
                        }
                    }

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
                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Join node '{Id}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, "next");
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToId, "edges.to");
                        }
                    }

                    break;
                case NodeKind.End:
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        /// <summary>
        /// ノード種別ごとに StateDefinition へ変換する。
        /// </summary>
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
                            ["Completed"] = BuildLinearTransitionForFact(Id, Next, Edges)
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
                            ["Completed"] = BuildLinearTransitionForFact(Id, Next, Edges)
                        }
                    };
                case NodeKind.Wait:
                    return new StateDefinition
                    {
                        Wait = new WaitDefinition { Event = WaitEvent! },
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Completed"] = BuildLinearTransitionForFact(Id, Next, Edges)
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
                            ["Joined"] = BuildLinearTransitionForFact(Id, Next, Edges)
                        }
                    };
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        /// <summary>
        /// ノード定義の edges を読み取り、条件遷移の素材となる内部モデルへ変換する。
        /// </summary>
        private static IReadOnlyList<NodeEdgeDefinition>? ParseEdges(string? nodeIdForErrors, Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("edges", out var edgesVal) || edgesVal is null)
            {
                return null;
            }

            if (edgesVal is not IList edgesList)
            {
                throw new ArgumentException(Format(nodeIdForErrors, "'edges' must be a list."));
            }

            if (edgesList.Count == 0)
            {
                throw new ArgumentException(Format(nodeIdForErrors, "'edges' must be non-empty when present."));
            }

            var result = new List<NodeEdgeDefinition>(edgesList.Count);
            foreach (var raw in edgesList)
            {
                if (raw is null)
                {
                    throw new ArgumentException(Format(nodeIdForErrors, "'edges' must not contain null entries."));
                }

                var edgeDict = ToStringDict(raw, StringComparer.OrdinalIgnoreCase);
                result.Add(NodeEdgeDefinition.Parse(nodeIdForErrors, edgeDict));
            }

            return result;
        }

        /// <summary>
        /// next / edges を統合し、on.&lt;Fact&gt; 用の遷移を組み立てる。
        /// 条件 edge がある場合は cases/default へ正規化する。
        /// </summary>
        private static TransitionDefinition BuildLinearTransitionForFact(
            string nodeId,
            string? next,
            IReadOnlyList<NodeEdgeDefinition>? edges)
        {
            if (edges is null || edges.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(next))
                {
                    throw new ArgumentException($"Node '{nodeId}': missing 'next'.");
                }

                return new TransitionDefinition { Next = next };
            }

            // edges のみ、または next と併記（単一無条件のみ）を受理する。
            var unconditional = edges.Where(e => e.IsUnconditional).ToList();
            var conditional = edges.Where(e => !e.IsUnconditional).ToList();

            if (conditional.Count == 0)
            {
                if (unconditional.Count != 1)
                {
                    throw new ArgumentException(
                        $"Node '{nodeId}': when using unconditional 'edges', exactly one edge is required (found {unconditional.Count}).");
                }

                var onlyTo = unconditional[0].ToId;
                if (!string.IsNullOrWhiteSpace(next)
                    && !string.Equals(next, onlyTo, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Node '{nodeId}': 'next' and unconditional 'edges[0].to' must match when both are set.");
                }

                return new TransitionDefinition { Next = onlyTo };
            }

            // 条件付き edges がある場合は cases/default へ正規化する。
            var defaultEdges = edges.Where(e => e.IsDefaultEdge || e.IsUnconditional).ToList();
            if (defaultEdges.Count != 1)
            {
                throw new ArgumentException(
                    $"Node '{nodeId}': conditional 'edges' require exactly one default/unconditional edge (found {defaultEdges.Count}).");
            }

            var defaultTo = defaultEdges[0].ToId;
            if (!string.IsNullOrWhiteSpace(next)
                && !string.Equals(next, defaultTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Node '{nodeId}': 'next' must match the default/unconditional edge target when conditional 'edges' are present.");
            }

            var cases = new List<TransitionCaseDefinition>(conditional.Count);
            foreach (var e in conditional)
            {
                cases.Add(
                    new TransitionCaseDefinition
                    {
                        Order = e.Order,
                        When = e.When ?? throw new InvalidOperationException($"Node '{nodeId}': internal error: conditional edge missing when."),
                        Transition = new TransitionDefinition { Next = e.ToId }
                    });
            }

            return new TransitionDefinition
            {
                Cases = cases,
                Default = new TransitionDefinition { Next = defaultTo }
            };
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

    /// <summary>
    /// nodes.edges の 1 要素を表す内部モデル。
    /// </summary>
    private readonly record struct NodeEdgeDefinition(
        string ToId,
        ConditionExpressionDefinition? When,
        int? Order,
        bool IsDefaultEdge)
    {
        public bool IsUnconditional => When is null;

        /// <summary>
        /// edge 定義を検証しつつ内部モデルへ変換する。
        /// </summary>
        public static NodeEdgeDefinition Parse(string? nodeId, Dictionary<string, object?> edgeDict)
        {
            var toId = ResolveEdgeTargetId(nodeId, edgeDict);

            edgeDict.TryGetValue("when", out var whenRaw);
            ConditionExpressionDefinition? when = null;
            if (whenRaw is not null)
            {
                var whenDict = ToStringDict(whenRaw, StringComparer.OrdinalIgnoreCase);
                when = ParseConditionWhen(whenDict, nodeId);
            }

            var order = GetNullableInt(edgeDict, "order");

            var isDefaultEdge = false;
            if (edgeDict.TryGetValue("default", out var defaultRaw) && defaultRaw is not null)
            {
                switch (defaultRaw)
                {
                    case bool b:
                        isDefaultEdge = b;
                        break;
                    case string s when bool.TryParse(s, out var pb):
                        isDefaultEdge = pb;
                        break;
                    default:
                        throw new ArgumentException(
                            Format(nodeId, "edge 'default' must be boolean true/false (use 'to' for the transition target)."));
                }
            }

            if (when is not null && isDefaultEdge)
            {
                throw new ArgumentException(Format(nodeId, "edge cannot specify both 'when' and 'default: true'."));
            }

            if (string.IsNullOrWhiteSpace(toId))
            {
                throw new ArgumentException(Format(nodeId, "edge requires non-empty 'to' (or 'to.id')."));
            }

            return new NodeEdgeDefinition(toId, when, order, isDefaultEdge);
        }

        /// <summary>
        /// edge.to（文字列 or オブジェクト）から遷移先ノード ID を解決する。
        /// </summary>
        private static string ResolveEdgeTargetId(string? nodeId, Dictionary<string, object?> edgeDict)
        {
            if (!edgeDict.TryGetValue("to", out var toRaw) || toRaw is null)
            {
                throw new ArgumentException(Format(nodeId, "edge requires 'to'."));
            }

            if (toRaw is string s)
            {
                return s;
            }

            var toDict = ToStringDict(toRaw, StringComparer.OrdinalIgnoreCase);
            var id = GetStr(toDict, "id");
            return id ?? "";
        }

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
