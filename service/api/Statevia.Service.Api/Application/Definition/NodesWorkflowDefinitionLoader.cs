using System.Collections;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Core.Engine.FSM;

namespace Statevia.Service.Api.Application.Definition;

using static NodesSchemaLiteral;

/// <summary>nodes（UI）形式ルート → <see cref="WorkflowDefinition"/>。</summary>
internal sealed class NodesWorkflowDefinitionLoader : WorkflowDefinitionLoaderBase
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

        if (root.ContainsKey(KeyControls))
        {
            throw new ArgumentException("Root 'controls' is not supported in MVP (see v2-nodes-to-states-conversion-spec §7).");
        }

        ValidateVersion(root);
        var workflowDict = GetChildDict(root, KeyWorkflow, StringComparer.OrdinalIgnoreCase);
        if (workflowDict.Count == 0)
        {
            throw new ArgumentException("Nodes workflow definition requires root 'workflow'.");
        }

        if (!root.TryGetValue(KeyNodes, out var nodesRaw) || nodesRaw is not IList nodesList || nodesList.Count == 0)
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
        var modules = ParseWorkflowModules(workflowDict);

        return new WorkflowDefinition
        {
            Name = name,
            Modules = modules,
            States = states,
        };
    }

    /// <summary>
    /// nodes 形式のバージョン互換を検証する。
    /// 現在は <c>version: 1</c> のみ受理する。
    /// </summary>
    private static void ValidateVersion(Dictionary<string, object?> root)
    {
        if (!root.TryGetValue(KeyVersion, out var v) || v == null)
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
        var name = GetStr(workflowDict, KeyName);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var id = GetStr(workflowDict, KeyId);
        return !string.IsNullOrWhiteSpace(id) ? id : "Unnamed";
    }

    /// <summary>
    /// ノード集合の構造制約（ID一意性、start/end 個数、参照整合）を検証する。
    /// </summary>
    private static void ValidateStructure(IReadOnlyList<ParsedNode> nodes)
    {
        var byName = new Dictionary<string, ParsedNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Name))
            {
                throw new ArgumentException("Every node must have a non-empty 'name'.");
            }

            if (!byName.TryAdd(n.Name, n))
            {
                throw new ArgumentException($"Duplicate node name (case-insensitive): '{n.Name}'.");
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
            n.ValidateForbiddenMvp();
            n.ValidateReferences(byName);
        }

        ValidateReachability(nodes, byName, starts[0]);
    }

    /// <summary>
    /// start からの到達可能性を幅優先探索で検証する。
    /// </summary>
    private static void ValidateReachability(
        IReadOnlyList<ParsedNode> nodes,
        Dictionary<string, ParsedNode> byName,
        ParsedNode start)
    {
        if (!start.OutNeighborNames().Any())
        {
            throw new ArgumentException($"Start node '{start.Name}' must have at least one outgoing transition ('next' or 'edges').");
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(start.Name);
        visited.Add(start.Name);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!byName.TryGetValue(name, out var n))
            {
                continue;
            }

            foreach (var targetName in n.OutNeighborNames().Where(visited.Add))
            {
                queue.Enqueue(targetName);
            }
        }

        var unreachable = nodes.FirstOrDefault(node => !visited.Contains(node.Name));
        if (unreachable is not null)
        {
            throw new ArgumentException($"Unreachable node from start: '{unreachable.Name}'.");
        }
    }

    /// <summary>
    /// nodes を states へ正規化し、状態名（node.id）をキーにした辞書を構築する。
    /// </summary>
    private static Dictionary<string, StateDefinition> BuildStates(IReadOnlyList<ParsedNode> nodes)
    {
        var byName = nodes.ToDictionary(n => n.Name, n => n, StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var n in nodes)
        {
            states[n.Name] = n.ToStateDefinition(byName, n);
        }

        return states;
    }

    /// <summary>
    /// nodes[] の単一要素を正規化した内部表現。
    /// 構文検証と states 変換の両方で使う。
    /// </summary>
    private sealed class ParsedNode
    {
        public required string Name { get; init; }
        public required NodeKind Kind { get; init; }
        public required Dictionary<string, object?> Raw { get; init; }
        public string? Next { get; init; }
        public List<NodeEdgeDefinition>? Edges { get; init; }
        public string? ActionId { get; init; }

        /// <summary>Wait のイベント名 → 遷移先ノード ID（Signal）。</summary>
        public IReadOnlyDictionary<string, string>? WaitEvents { get; init; }

        /// <summary>Wait.subscribe 購読（Subscribe）。</summary>
        public IReadOnlyList<WaitSubscribeEntry>? WaitSubscribe { get; init; }

        /// <summary>Wait の担当者枠（Phase 2。構文保持のみ）。</summary>
        public IReadOnlyList<string>? WaitAssignees { get; init; }

        public string? Error { get; init; }
        public IReadOnlyList<string>? Branches { get; init; }
        public object? InputRaw { get; init; }

        /// <summary>action 完了時に vars へ書き込むパス（YAML <c>output</c>）。</summary>
        public string? Output { get; init; }

        /// <summary>
        /// 生のノード辞書を型付き表現へ変換する。
        /// </summary>
        public static ParsedNode FromDict(Dictionary<string, object?> dict)
        {
            var name = GetStr(dict, KeyName);
            var typeStr = GetStr(dict, KeyType);
            if (string.IsNullOrWhiteSpace(typeStr))
            {
                throw new ArgumentException("Every node must have 'type'.");
            }

#pragma warning disable CA1308 // YAML の type 値は小文字キーワードとして正規化する
            var kind = typeStr.Trim().ToLowerInvariant() switch
#pragma warning restore CA1308
            {
                NodeTypeStart => NodeKind.Start,
                NodeTypeEnd => NodeKind.End,
                NodeTypeAction => NodeKind.Action,
                NodeTypeWait => NodeKind.Wait,
                NodeTypeFork => NodeKind.Fork,
                NodeTypeJoin => NodeKind.Join,
                _ => throw new ArgumentException($"Unknown node type '{typeStr}' for node '{name ?? "?"}'.")
            };

            var next = GetStr(dict, KeyNext);
            var action = GetStr(dict, KeyAction);
            var error = ResolveNodeTargetName(dict, name, KeyError);
            var branches = GetStrList(dict, KeyBranches);
            dict.TryGetValue(KeyInput, out var inputVal);
            var output = GetStr(dict, KeyOutput);
            var edges = ParseEdges(name, dict);
            var (waitEvents, waitSubscribe, waitAssignees) = kind == NodeKind.Wait
                ? ParseWaitPayload(name, dict, next)
                : (null, null, null);

            return new ParsedNode
            {
                Name = name ?? throw new ArgumentException("Every node must have 'name'."),
                Kind = kind,
                Raw = dict,
                Next = next,
                Edges = edges,
                ActionId = action,
                WaitEvents = waitEvents,
                WaitSubscribe = waitSubscribe,
                WaitAssignees = waitAssignees,
                Error = error,
                Branches = branches,
                InputRaw = inputVal,
                Output = output
            };
        }

        /// <summary>
        /// Wait ノードの <c>events</c> / <c>subscribe</c> / 旧 <c>event</c>+<c>next</c> を正規化する。
        /// </summary>
        private static (
            IReadOnlyDictionary<string, string>? Events,
            IReadOnlyList<WaitSubscribeEntry>? Subscribe,
            IReadOnlyList<string>? Assignees) ParseWaitPayload(
            string? nodeName,
            Dictionary<string, object?> dict,
            string? next)
        {
            if (HasKeyIgnoreCase(dict, KeyExits))
            {
                throw new ArgumentException(
                    $"Wait node '{nodeName ?? "?"}' must not use 'exits'; use 'events' or 'subscribe'.");
            }

            var assignees = GetStrList(dict, KeyAssignees);
            var hasEvents = HasKeyIgnoreCase(dict, KeyEvents);
            var hasSubscribe = HasKeyIgnoreCase(dict, KeySubscribe);
            var legacyEvent = GetStr(dict, KeyEvent);

            if (hasEvents && hasSubscribe)
            {
                throw new ArgumentException(
                    $"Wait node '{nodeName ?? "?"}' cannot use both 'events' and 'subscribe'.");
            }

            if (hasEvents && !string.IsNullOrWhiteSpace(legacyEvent))
            {
                throw new ArgumentException(
                    $"Wait node '{nodeName ?? "?"}' cannot use both 'events' and 'event'.");
            }

            if (hasSubscribe)
            {
                var subscribeKey = dict.Keys.First(k => string.Equals(k, KeySubscribe, StringComparison.OrdinalIgnoreCase));
                return (null, ParseSubscribeList(nodeName, dict[subscribeKey]), assignees);
            }

            if (hasEvents)
            {
                var eventsKey = dict.Keys.First(k => string.Equals(k, KeyEvents, StringComparison.OrdinalIgnoreCase));
                return (ParseEventsMap(nodeName, dict[eventsKey]), null, assignees);
            }

            if (string.IsNullOrWhiteSpace(legacyEvent))
            {
                throw new ArgumentException(
                    $"Wait node '{nodeName ?? "?"}' must have 'events', 'subscribe', or 'event'.");
            }

            // 旧形式: event + next → events
            return (
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [legacyEvent.Trim()] = next?.Trim() ?? string.Empty
                },
                null,
                assignees);
        }

        /// <summary>nodes 形式の subscribe 配列を読み取る。</summary>
        private static List<WaitSubscribeEntry> ParseSubscribeList(string? nodeName, object? subscribeVal)
        {
            if (subscribeVal is not System.Collections.IEnumerable enumerable || subscribeVal is string)
            {
                throw new ArgumentException($"Wait node '{nodeName ?? "?"}' subscribe must be a list.");
            }

            var result = new List<WaitSubscribeEntry>();
            foreach (var item in enumerable)
            {
                if (item == null)
                    continue;

                var dict = ToStringDict(item);
                var topic = GetStr(dict, KeyTopic);
                var key = GetStr(dict, KeyKey);
                var next = GetStr(dict, KeyNext);
                result.Add(new WaitSubscribeEntry
                {
                    Topic = topic?.Trim() ?? string.Empty,
                    Key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim(),
                    Next = next?.Trim() ?? string.Empty
                });
            }

            if (result.Count == 0)
            {
                throw new ArgumentException($"Wait node '{nodeName ?? "?"}' subscribe must not be empty.");
            }

            return result;
        }

        /// <summary>
        /// nodes 形式の <c>events</c> マップを読み取る。
        /// </summary>
        private static Dictionary<string, string> ParseEventsMap(string? nodeName, object? eventsVal)
        {
            if (eventsVal == null)
            {
                throw new ArgumentException($"Wait node '{nodeName ?? "?"}' has empty 'events'.");
            }

            var eventsDict = ToStringDict(eventsVal);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (eventName, rawNext) in eventsDict)
            {
                if (string.IsNullOrWhiteSpace(eventName))
                {
                    throw new ArgumentException(
                        $"Wait node '{nodeName ?? "?"}' events keys must be non-empty.");
                }

                var target = rawNext?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(target))
                {
                    throw new ArgumentException(
                        $"Wait node '{nodeName ?? "?"}' events['{eventName}'] must specify a next node name.");
                }

                if (!result.TryAdd(eventName.Trim(), target))
                {
                    throw new ArgumentException(
                        $"Wait node '{nodeName ?? "?"}' events contains duplicate event '{eventName}'.");
                }
            }

            if (result.Count == 0)
            {
                throw new ArgumentException($"Wait node '{nodeName ?? "?"}' must have a non-empty 'events' map.");
            }

            return result;
        }

        /// <summary>
        /// 到達性検証に使う隣接ノード ID を列挙する。
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Critical Code Smell",
            "S3776:Cognitive Complexity of methods should not be too high",
            Justification = "ノード種別ごとの隣接 ID 列挙を switch で網羅している。")]
        public IEnumerable<string> OutNeighborNames()
        {
            switch (Kind)
            {
                case NodeKind.Start:
                case NodeKind.Action:
                case NodeKind.Join:
                    if (!string.IsNullOrEmpty(Next))
                    {
                        yield return Next;
                    }

                    if (Edges != null)
                    {
                        foreach (var toName in Edges
                                     .Select(edge => edge.ToName)
                                     .Where(toName => !string.IsNullOrWhiteSpace(toName)))
                        {
                            yield return toName;
                        }
                    }

                    if (Kind == NodeKind.Action && !string.IsNullOrWhiteSpace(Error))
                    {
                        yield return Error;
                    }

                    break;
                case NodeKind.Wait:
                    if (WaitSubscribe != null)
                    {
                        foreach (var targetName in WaitSubscribe
                                     .Select(entry => entry.Next)
                                     .Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            yield return targetName;
                        }
                    }

                    if (WaitEvents != null)
                    {
                        foreach (var targetName in WaitEvents.Values.Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            yield return targetName;
                        }
                    }

                    if (!string.IsNullOrEmpty(Next))
                    {
                        yield return Next;
                    }

                    if (Edges != null)
                    {
                        foreach (var toName in Edges
                                     .Select(edge => edge.ToName)
                                     .Where(toName => !string.IsNullOrWhiteSpace(toName)))
                        {
                            yield return toName;
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
        /// Join 供給に使う後続。Fork 枝と <c>error</c> は含めない。
        /// </summary>
        public IEnumerable<string> SupplyNeighborNames()
        {
            if (Kind is NodeKind.Fork or NodeKind.End)
            {
                yield break;
            }

            foreach (var neighbor in OutNeighborNames())
            {
                if (Kind == NodeKind.Action
                    && !string.IsNullOrWhiteSpace(Error)
                    && string.Equals(neighbor, Error, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return neighbor;
            }
        }

        /// <summary>
        /// MVP で未対応のノード属性を拒否する。
        /// </summary>
        public void ValidateForbiddenMvp()
        {
            switch (Kind)
            {
                case NodeKind.Start:
                    ForbidKeys(Name, Raw, KeyError);
                    break;
                case NodeKind.End:
                    ValidateEndForbiddenMvp();
                    break;
                case NodeKind.Action:
                    break;
                case NodeKind.Fork:
                    ValidateForkForbiddenMvp();
                    break;
                case NodeKind.Wait:
                    ValidateWaitForbiddenMvp();
                    break;
                case NodeKind.Join:
                    ValidateJoinForbiddenMvp();
                    break;
            }
        }

        /// <summary>end ノードの MVP 禁止属性を検証する。</summary>
        private void ValidateEndForbiddenMvp()
        {
            ForbidKeys(Name, Raw, KeyError);
            if (HasKeyIgnoreCase(Raw, KeyNext))
            {
                throw new ArgumentException($"Node '{Name}': 'type: end' must not have 'next' (§3.1).");
            }

            if (Edges is { Count: > 0 })
            {
                throw new ArgumentException($"Node '{Name}': 'type: end' must not have 'edges' (§3.1).");
            }
        }

        /// <summary>fork ノードの MVP 禁止属性を検証する。</summary>
        private void ValidateForkForbiddenMvp()
        {
            ForbidKeys(Name, Raw, KeyError);
            if (Edges is { Count: > 0 })
            {
                throw new ArgumentException($"Fork node '{Name}': 'edges' is not supported in MVP.");
            }
        }

        /// <summary>
        /// wait ノードの MVP 禁止属性を検証する。
        /// </summary>
        /// <remarks>新形式 <c>events</c> の遷移先はマップが正本のため、<c>edges</c> との二重定義を拒否する。</remarks>
        private void ValidateWaitForbiddenMvp()
        {
            ForbidKeys(Name, Raw, KeyError);
            if (HasKeyIgnoreCase(Raw, KeyEvents)
                && (HasKeyIgnoreCase(Raw, KeyEdges) || Edges is { Count: > 0 }))
            {
                throw new ArgumentException(
                    $"Wait node '{Name}': 'edges' is not supported with 'events'; put targets in 'events'.");
            }
        }

        /// <summary>join ノードの MVP 禁止属性を検証する。</summary>
        private void ValidateJoinForbiddenMvp()
        {
            ForbidKeys(Name, Raw, KeyError);
            if (!Raw.TryGetValue(KeyMode, out var modeVal) || modeVal == null)
            {
                return;
            }

            var m = modeVal.ToString()?.Trim();
            if (!string.Equals(m, JoinModeAll, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Join '{Name}': only mode 'all' is supported in MVP (found '{m}').");
            }
        }

        /// <summary>
        /// ノード種別ごとの必須属性と参照先 ID の存在を検証する。
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Critical Code Smell",
            "S3776:Cognitive Complexity of methods should not be too high",
            Justification = "ノード種別ごとの参照検証を switch で網羅している。")]
        public void ValidateReferences(Dictionary<string, ParsedNode> byName)
        {
            void MustExist(string? refName, string role)
            {
                if (string.IsNullOrEmpty(refName))
                {
                    return;
                }

                if (!byName.ContainsKey(refName))
                {
                    throw new ArgumentException($"Node '{Name}': {role} references unknown name '{refName}'.");
                }
            }

            switch (Kind)
            {
                case NodeKind.Start:
                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Start node '{Name}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, KeyNext);
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToName, KeyEdgesTo);
                        }
                    }

                    break;
                case NodeKind.Action:
                    if (string.IsNullOrWhiteSpace(ActionId))
                    {
                        throw new ArgumentException($"Action node '{Name}' must have 'action'.");
                    }

                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Action node '{Name}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, KeyNext);
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToName, KeyEdgesTo);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(Error))
                    {
                        MustExist(Error, KeyError);
                        if (string.Equals(Error, Name, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException($"Action node '{Name}' must not self-reference with 'error'.");
                        }
                    }

                    break;
                case NodeKind.Wait:
                    var hasSubscribe = WaitSubscribe is { Count: > 0 };
                    var hasSignalEvents = WaitEvents is { Count: > 0 };
                    if (!hasSubscribe && !hasSignalEvents)
                    {
                        throw new ArgumentException(
                            $"Wait node '{Name}' must have 'events', 'subscribe', or 'event'.");
                    }

                    if (hasSubscribe)
                    {
                        for (var i = 0; i < WaitSubscribe!.Count; i++)
                        {
                            var entry = WaitSubscribe[i];
                            if (string.IsNullOrWhiteSpace(entry.Topic))
                            {
                                throw new ArgumentException(
                                    $"Wait node '{Name}' subscribe[{i}].topic must not be empty.");
                            }

                            MustExist(entry.Next, $"{KeySubscribe}[{i}].{KeyNext}");
                            if (string.Equals(entry.Next, Name, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException(
                                    $"Wait node '{Name}' must not self-reference via subscribe[{i}].");
                            }
                        }

                        break;
                    }

                    var hasLegacyEvent = HasKeyIgnoreCase(Raw, KeyEvent);
                    if (hasLegacyEvent
                        && string.IsNullOrWhiteSpace(Next)
                        && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Wait node '{Name}' must have 'next' or 'edges'.");
                    }

                    if (!hasLegacyEvent)
                    {
                        foreach (var (eventName, targetName) in WaitEvents!)
                        {
                            MustExist(targetName, $"{KeyEvents}.{eventName}");
                            if (string.Equals(targetName, Name, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException(
                                    $"Wait node '{Name}' must not self-reference via events['{eventName}'].");
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(Next))
                        {
                            MustExist(Next, KeyNext);
                        }

                        if (Edges != null)
                        {
                            foreach (var e in Edges)
                            {
                                MustExist(e.ToName, KeyEdgesTo);
                            }
                        }
                    }

                    break;
                case NodeKind.Fork:
                    if (Branches == null || Branches.Count < 2)
                    {
                        throw new ArgumentException($"Fork node '{Name}' must have 'branches' with at least 2 names.");
                    }

                    foreach (var b in Branches)
                    {
                        MustExist(b, KeyBranches);
                    }

                    break;
                case NodeKind.Join:
                    if (string.IsNullOrWhiteSpace(Next) && (Edges == null || Edges.Count == 0))
                    {
                        throw new ArgumentException($"Join node '{Name}' must have 'next' or 'edges'.");
                    }

                    if (!string.IsNullOrWhiteSpace(Next))
                    {
                        MustExist(Next, KeyNext);
                    }

                    if (Edges != null)
                    {
                        foreach (var e in Edges)
                        {
                            MustExist(e.ToName, KeyEdgesTo);
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
        /// Join に対応する Fork の枝集合を解決する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 各枝先頭が当該 Join を「供給」する一意の Fork を選ぶ。
        /// 供給は (1) <c>next</c> / <c>wait.events</c> / <c>edges</c> の 1 経路、または
        /// (2) 枝先頭または枝内の内側 Fork で、その内側 Join の出口（他 Join を跨がない）が当該 Join に到達すること。
        /// 3 段以上のネストでは、一段外側の Join への供給は「その段のペア Join の出口」だけが担う。
        /// <c>error</c> は供給に数えない。
        /// </para>
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Critical Code Smell",
            "S3776:Cognitive Complexity of methods should not be too high",
            Justification = "fork/join（ネスト含む）の照合を単一メソッドに集約している。")]
        private IReadOnlyList<string> ResolveJoinAll(IReadOnlyDictionary<string, ParsedNode> byName)
        {
            var joinName = Name;
            var supplySuccessors = BuildSupplySuccessors(byName);
            var joinBarriers = byName.Values
                .Where(static n => n.Kind == NodeKind.Join)
                .Select(static n => n.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(string ForkName, IReadOnlyList<string> Branches)>();

            foreach (var n in byName.Values)
            {
                if (n.Kind != NodeKind.Fork || n.Branches == null || n.Branches.Count < 2)
                {
                    continue;
                }

                if (ForkFeedsJoin(n, joinName, byName, supplySuccessors, joinBarriers, []))
                {
                    candidates.Add((n.Name, n.Branches));
                }
            }

            if (candidates.Count == 0)
            {
                throw new ArgumentException(
                    $"Join '{joinName}' has no matching fork whose branches feed this join " +
                    "(next, wait.events, or edges; or nested fork whose inner join exits to this join).");
            }

            if (candidates.Count > 1)
            {
                var names = string.Join(", ", candidates.Select(c => c.ForkName));
                throw new ArgumentException(
                    $"Join '{joinName}' matches multiple forks ({names}). A join must pair with a unique fork.");
            }

            return candidates[0].Branches;
        }

        /// <summary>Fork の全枝が <paramref name="joinName"/> を供給するか。</summary>
        private static bool ForkFeedsJoin(
            ParsedNode fork,
            string joinName,
            IReadOnlyDictionary<string, ParsedNode> byName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visitingForks)
        {
            if (fork.Branches is null || fork.Branches.Count < 2)
                return false;

            foreach (var branchName in fork.Branches)
            {
                if (!byName.TryGetValue(branchName, out var branchHead) || branchHead.Kind == NodeKind.Join)
                    return false;

                if (!BranchFeedsJoin(branchHead, joinName, byName, supplySuccessors, joinBarriers, visitingForks))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 枝先頭が Join を供給するか（<c>next</c> / <c>wait.events</c> / <c>edges</c>、
        /// または枝先頭・枝内のネスト Fork → 内側 Join の出口）。
        /// </summary>
        private static bool BranchFeedsJoin(
            ParsedNode branchHead,
            string joinName,
            IReadOnlyDictionary<string, ParsedNode> byName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visitingForks)
        {
            if (JoinSupply.Reaches(supplySuccessors, branchHead.Name, joinName, joinBarriers))
                return true;

            return ForkOnSupplyPathExitsTo(
                branchHead, joinName, byName, supplySuccessors, joinBarriers, visitingForks);
        }

        /// <summary>
        /// 供給経路上（Join 障壁まで）の内側 Fork が、ペア Join の出口で <paramref name="joinName"/> を供給するか。
        /// </summary>
        /// <remarks>
        /// 枝先頭が Fork のネストに加え、action 等の後に内側 Fork がある形
        ///（<c>mid → inner.fork → inner.join → outer.join</c>）も外側 Join の供給とみなす。
        /// Fork の枝は線形供給に含めない（内側 Join 出口で正規化する）。
        /// </remarks>
        private static bool ForkOnSupplyPathExitsTo(
            ParsedNode start,
            string joinName,
            IReadOnlyDictionary<string, ParsedNode> byName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visitingForks)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var visited = new HashSet<string>(comparer) { start.Name };
            var queue = new Queue<string>();
            queue.Enqueue(start.Name);

            while (queue.Count > 0)
            {
                var currentName = queue.Dequeue();
                if (!byName.TryGetValue(currentName, out var current))
                    continue;

                if (TryNestedForkExit(current, joinName, byName, supplySuccessors, joinBarriers, visitingForks))
                    return true;

                EnqueueSupplyNeighbors(currentName, joinName, supplySuccessors, joinBarriers, visited, queue);
            }

            return false;
        }

        /// <summary>現在ノードが Fork なら、ペア Join の出口が対象 Join に着くか。</summary>
        private static bool TryNestedForkExit(
            ParsedNode current,
            string joinName,
            IReadOnlyDictionary<string, ParsedNode> byName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visitingForks)
        {
            if (current.Kind != NodeKind.Fork || current.Branches is null)
                return false;

            if (!visitingForks.Add(current.Name))
                return false;

            var pairedInnerJoin = TryFindUniqueJoinPairedWithFork(
                current, byName, supplySuccessors, joinBarriers, visitingForks);
            return pairedInnerJoin is not null
                && InnerJoinExitsTo(pairedInnerJoin, joinName, supplySuccessors, joinBarriers);
        }

        /// <summary>Join 障壁と対象 Join を除いた供給後続を走査キューへ載せる。</summary>
        private static void EnqueueSupplyNeighbors(
            string currentName,
            string joinName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visited,
            Queue<string> queue)
        {
            if (!supplySuccessors.TryGetValue(currentName, out var nexts))
                return;

            foreach (var next in nexts
                         .Where(n =>
                             !string.IsNullOrWhiteSpace(n)
                             && !string.Equals(n, joinName, StringComparison.OrdinalIgnoreCase)
                             && !joinBarriers.Contains(n))
                         .Where(visited.Add))
            {
                queue.Enqueue(next);
            }
        }

        /// <summary>
        /// 内側 Join の出口が <paramref name="joinName"/> に着くか。
        /// </summary>
        /// <remarks>
        /// 出口が他 Join のときは、その Join とペアの Fork が外側への供給を担う。
        /// ここから他 Join の <c>next</c> を辿ると、3 段ネストで中間 Fork が外側 Join にも
        /// 供給していると誤判定し、ペアが一意でなくなって <c>outer.join</c> が解決できない。
        /// 非 Join ノード（通知 action など）経由の到達は従来どおり許す。
        /// </remarks>
        /// <param name="innerJoin">ネスト Fork とペアの内側 Join。</param>
        /// <param name="joinName">供給先として判定する Join 名。</param>
        /// <param name="supplySuccessors">Join 供給用の後続マップ。</param>
        /// <param name="joinBarriers">他 Join 名の集合（横断禁止）。</param>
        /// <returns>出口が <paramref name="joinName"/> に着くとき true。</returns>
        private static bool InnerJoinExitsTo(
            ParsedNode innerJoin,
            string joinName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers)
        {
            if (!supplySuccessors.TryGetValue(innerJoin.Name, out var exits) || exits.Count == 0)
                return false;

            return exits.Any(exit =>
                string.Equals(exit, joinName, StringComparison.OrdinalIgnoreCase)
                || (!joinBarriers.Contains(exit)
                    && JoinSupply.Reaches(supplySuccessors, exit, joinName, joinBarriers)));
        }

        /// <summary>指定 Fork と一意にペアになる Join（全枝が当該 Join を供給）を探す。</summary>
        private static ParsedNode? TryFindUniqueJoinPairedWithFork(
            ParsedNode fork,
            IReadOnlyDictionary<string, ParsedNode> byName,
            Dictionary<string, IReadOnlyList<string>> supplySuccessors,
            IReadOnlySet<string> joinBarriers,
            HashSet<string> visitingForks)
        {
            ParsedNode? found = null;
            foreach (var candidate in byName.Values)
            {
                if (candidate.Kind != NodeKind.Join)
                    continue;

                if (!ForkFeedsJoin(fork, candidate.Name, byName, supplySuccessors, joinBarriers, visitingForks))
                    continue;

                if (found is not null)
                    return null;

                found = candidate;
            }

            return found;
        }

        /// <summary>
        /// Join 供給用の後続マップ。<c>error</c> と Fork 枝は含めない（ネストは内側 Join 出口で辿る）。
        /// </summary>
        private static Dictionary<string, IReadOnlyList<string>> BuildSupplySuccessors(
            IReadOnlyDictionary<string, ParsedNode> byName)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, node) in byName)
            {
                map[name] = node.SupplyNeighborNames()
                    .Where(static n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return map;
        }

        /// <summary>
        /// ノード種別ごとに StateDefinition へ変換する。
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Critical Code Smell",
            "S3776:Cognitive Complexity of methods should not be too high",
            Justification = "ノード種別ごとの states 変換を switch で網羅している。")]
        public StateDefinition ToStateDefinition(IReadOnlyDictionary<string, ParsedNode> byName, ParsedNode self)
        {
            switch (Kind)
            {
                case NodeKind.Start:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            [Fact.Completed] = BuildLinearTransitionForFact(Name, Next, Edges)
                        }
                    };
                case NodeKind.End:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            [Fact.Completed] = new TransitionDefinition { End = true }
                        }
                    };
                case NodeKind.Action:
                    var transitions = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [Fact.Completed] = BuildLinearTransitionForFact(Name, Next, Edges)
                    };
                    if (!string.IsNullOrWhiteSpace(Error))
                    {
                        transitions[Fact.Failed] = new TransitionDefinition { Next = Error };
                    }

                    return new StateDefinition
                    {
                        Action = ActionId,
                        Input = ParseActionInput(Name, InputRaw),
                        Output = Output,
                        Retry = ParseRetryDefinition(Raw),
                        On = transitions
                    };
                case NodeKind.Wait:
                    {
                        if (WaitSubscribe is { Count: > 0 })
                        {
                            return new StateDefinition
                            {
                                Wait = new WaitDefinition
                                {
                                    Subscribe = WaitSubscribe,
                                    Assignees = WaitAssignees
                                }
                            };
                        }

                        // 旧形式（event+next）は On.Completed を維持（ランタイム移行完了まで）。
                        // events 正本は WaitDefinition.Events。単一イベント時は Completed 遷移も埋める。
                        var waitEvents = WaitEvents
                            ?? throw new InvalidOperationException($"Wait node '{Name}' has no events.");
                        Dictionary<string, TransitionDefinition>? on = null;
                        if (HasKeyIgnoreCase(Raw, KeyEvent) || waitEvents.Count == 1)
                        {
                            var fallbackNext = Next;
                            if (string.IsNullOrWhiteSpace(fallbackNext) && waitEvents.Count == 1)
                            {
                                fallbackNext = waitEvents.Values.First();
                            }

                            on = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                            {
                                [Fact.Completed] = BuildLinearTransitionForFact(Name, fallbackNext, Edges)
                            };
                        }

                        return new StateDefinition
                        {
                            Wait = new WaitDefinition
                            {
                                Events = waitEvents,
                                Assignees = WaitAssignees
                            },
                            On = on
                        };
                    }
                case NodeKind.Fork:
                    return new StateDefinition
                    {
                        Action = null,
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            [Fact.Completed] = new TransitionDefinition { Fork = Branches!.ToList() }
                        }
                    };
                case NodeKind.Join:
                    return new StateDefinition
                    {
                        Join = new JoinDefinition { All = ResolveJoinAll(byName).ToList() },
                        On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            [Fact.Joined] = BuildLinearTransitionForFact(Name, Next, Edges)
                        }
                    };
                default:
                    throw new InvalidOperationException($"Unhandled node kind: {Kind}");
            }
        }

        /// <summary>
        /// ノード定義の edges を読み取り、条件遷移の素材となる内部モデルへ変換する。
        /// </summary>
        private static List<NodeEdgeDefinition>? ParseEdges(string? nodeNameForErrors, Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue(KeyEdges, out var edgesVal) || edgesVal is null)
            {
                return null;
            }

            if (edgesVal is not IList edgesList)
            {
                throw new ArgumentException(Format(nodeNameForErrors, "'edges' must be a list."));
            }

            if (edgesList.Count == 0)
            {
                throw new ArgumentException(Format(nodeNameForErrors, "'edges' must be non-empty when present."));
            }

            var result = new List<NodeEdgeDefinition>(edgesList.Count);
            foreach (var raw in edgesList)
            {
                if (raw is null)
                {
                    throw new ArgumentException(Format(nodeNameForErrors, "'edges' must not contain null entries."));
                }

                var edgeDict = ToStringDict(raw, StringComparer.OrdinalIgnoreCase);
                result.Add(NodeEdgeDefinition.Parse(nodeNameForErrors, edgeDict));
            }

            return result;
        }

        /// <summary>
        /// next / edges を統合し、on.&lt;Fact&gt; 用の遷移を組み立てる。
        /// 条件 edge がある場合は cases/default へ正規化する。
        /// </summary>
        private static TransitionDefinition BuildLinearTransitionForFact(
            string nodeName,
            string? next,
            IReadOnlyList<NodeEdgeDefinition>? edges)
        {
            if (edges is null || edges.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(next))
                {
                    throw new ArgumentException($"Node '{nodeName}': missing 'next'.");
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
                        $"Node '{nodeName}': when using unconditional 'edges', exactly one edge is required (found {unconditional.Count}).");
                }

                var onlyTo = unconditional[0].ToName;
                if (!string.IsNullOrWhiteSpace(next)
                    && !string.Equals(next, onlyTo, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Node '{nodeName}': 'next' and unconditional 'edges[0].to' must match when both are set.");
                }

                return new TransitionDefinition { Next = onlyTo };
            }

            // 条件付き edges がある場合は cases/default へ正規化する。
            var defaultEdges = edges.Where(e => e.IsDefaultEdge || e.IsUnconditional).ToList();
            if (defaultEdges.Count != 1)
            {
                throw new ArgumentException(
                    $"Node '{nodeName}': conditional 'edges' require exactly one default/unconditional edge (found {defaultEdges.Count}).");
            }

            var defaultTo = defaultEdges[0].ToName;
            if (!string.IsNullOrWhiteSpace(next)
                && !string.Equals(next, defaultTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Node '{nodeName}': 'next' must match the default/unconditional edge target when conditional 'edges' are present.");
            }

            var cases = conditional
                .Select(edge => new TransitionCaseDefinition
                {
                    Order = edge.Order,
                    When = edge.When ?? throw new InvalidOperationException($"Node '{nodeName}': internal error: conditional edge missing when."),
                    Transition = new TransitionDefinition { Next = edge.ToName }
                })
                .ToList();

            return new TransitionDefinition
            {
                Cases = cases,
                Default = new TransitionDefinition { Next = defaultTo }
            };
        }

        private static void ForbidKeys(string nodeName, Dictionary<string, object?> raw, params string[] keys)
        {
            var forbidden = keys.FirstOrDefault(k => HasKeyIgnoreCase(raw, k));
            if (forbidden is null)
            {
                return;
            }

            throw new ArgumentException(
                $"Node '{nodeName}': '{forbidden}' is not supported in MVP (see v2-nodes-to-states-conversion-spec §7).");
        }

        private static StateInputDefinition? ParseActionInput(string nodeName, object? inputVal) =>
            ParseStrictInputMapping(inputVal, nodeName);

        private static string? ResolveNodeTargetName(Dictionary<string, object?> dict, string? nodeName, string key)
        {
            if (!dict.TryGetValue(key, out var raw) || raw is null)
            {
                return null;
            }

            if (raw is string s)
            {
                var trimmed = s.Trim();
                if (trimmed.Length == 0)
                {
                    throw new ArgumentException(Format(nodeName, $"'{key}' must be non-empty string or object with name."));
                }
                return trimmed;
            }

            var targetDict = ToStringDict(raw, StringComparer.OrdinalIgnoreCase);
            var name = GetStr(targetDict, KeyName);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(Format(nodeName, $"'{key}' object must include non-empty 'name'."));
            }
            return name;
        }
    }

    /// <summary>
    /// nodes.edges の 1 要素を表す内部モデル。
    /// </summary>
    private readonly record struct NodeEdgeDefinition(
        string ToName,
        ConditionExpressionDefinition? When,
        int? Order,
        bool IsDefaultEdge)
    {
        public bool IsUnconditional => When is null;

        /// <summary>
        /// edge 定義を検証しつつ内部モデルへ変換する。
        /// </summary>
        public static NodeEdgeDefinition Parse(string? nodeName, Dictionary<string, object?> edgeDict)
        {
            var toName = ResolveEdgeTargetName(nodeName, edgeDict);

            edgeDict.TryGetValue(KeyWhen, out var whenRaw);
            ConditionExpressionDefinition? when = null;
            if (whenRaw is not null)
            {
                var whenDict = ToStringDict(whenRaw, StringComparer.OrdinalIgnoreCase);
                when = ParseConditionWhen(whenDict, nodeName);
            }

            var order = GetNullableInt(edgeDict, KeyOrder);

            var isDefaultEdge = false;
            if (edgeDict.TryGetValue(KeyDefault, out var defaultRaw) && defaultRaw is not null)
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
                            Format(nodeName, "edge 'default' must be boolean true/false (use 'to' for the transition target)."));
                }
            }

            if (when is not null && isDefaultEdge)
            {
                throw new ArgumentException(Format(nodeName, "edge cannot specify both 'when' and 'default: true'."));
            }

            if (string.IsNullOrWhiteSpace(toName))
            {
                throw new ArgumentException(Format(nodeName, "edge requires non-empty 'to' (or 'to.name')."));
            }

            return new NodeEdgeDefinition(toName, when, order, isDefaultEdge);
        }

        /// <summary>
        /// edge.to（文字列 or オブジェクト）から遷移先ノード名を解決する。
        /// </summary>
        private static string ResolveEdgeTargetName(string? nodeName, Dictionary<string, object?> edgeDict)
        {
            if (!edgeDict.TryGetValue(KeyTo, out var toRaw) || toRaw is null)
            {
                throw new ArgumentException(Format(nodeName, "edge requires 'to'."));
            }

            if (toRaw is string s)
            {
                return s;
            }

            var toDict = ToStringDict(toRaw, StringComparer.OrdinalIgnoreCase);
            var name = GetStr(toDict, KeyName);
            return name ?? "";
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
