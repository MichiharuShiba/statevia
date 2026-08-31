using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.ExecutionGraphs;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 親実行グラフと物理子グラフを、論理 Fork/Join に見えるよう合成する（D6・GET 時）。
/// </summary>
/// <remarks>
/// <para>永続スナップショットは変更しない。読み取り専用の合成 JSON を返す。</para>
/// <para>子ノードの <c>workerId</c> / <c>attempt</c> はそのまま引き継ぐ。</para>
/// <para>
/// <c>forkNodeId</c> は Hosted 物理 Fork の並列エピソード相関キー（親グラフの Fork 到達
/// <c>nodeId</c>）。定義状態名でも Join <c>nodeId</c> でもない。Join 辺は状態名ではなく
/// <c>joinNodeId</c>（親上の Join 到達インスタンス）に固定し、循環で同名 Join が複数あるとき
/// Fork 訪問との対応は <see cref="ResolveJoinNodeId"/> が親グラフ上で解決する。
/// </para>
/// <para>
/// 定義上の枝以外の見た目の合流を避けるため、親 Fork からの Fork 辺は
/// <c>branchState</c> ノードへだけ張り、親 Join への Join 辺も枝先頭状態の終端に限る。
/// ネスト内側 Join など枝先頭以外から親 Join への接続は <see cref="EdgeType.Next"/> とする。
/// 枝先頭が Fork 型の terminal になった場合も親 Join へは繋がない（幽霊枝防止）。
/// </para>
/// </remarks>
internal static class PhysicalForkGraphComposer
{
    private const string NodeIdProperty = "nodeId";
    private const string NodeNameProperty = "nodeName";
    private const string NodeTypeProperty = "nodeType";
    private const string JoinNodeType = "Join";

    /// <summary>1 分岐分の子グラフ（再帰合成済み可）。</summary>
    /// <param name="ForkNodeId">親上の Fork 到達ノード ID。</param>
    /// <param name="JoinNodeId">親上の Join 到達ノード ID。未解決時は空（Join 辺を張らない）。</param>
    /// <param name="BranchState">定義上の分岐先頭状態名（<c>execution_branches.branch_state</c>）。</param>
    /// <param name="ChildGraphJson">子（または孫まで合成済み）の graph JSON。</param>
    internal sealed record BranchGraph(
        string ForkNodeId,
        string JoinNodeId,
        string BranchState,
        string ChildGraphJson);

    /// <summary>
    /// 親グラフへ子グラフをマージし、Fork / Join 辺を補完する。
    /// </summary>
    /// <param name="parentGraphJson">親の永続 graph JSON。</param>
    /// <param name="branches">親直下の分岐グラフ。</param>
    /// <returns>合成後 JSON。分岐が空なら親をそのまま返す。</returns>
    public static string Compose(string parentGraphJson, IReadOnlyList<BranchGraph> branches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentGraphJson);
        ArgumentNullException.ThrowIfNull(branches);
        if (branches.Count == 0)
            return parentGraphJson;

        if (!TryParseGraph(parentGraphJson, out var parentNodes, out var parentEdges))
            return parentGraphJson;

        var nodeById = IndexNodesById(parentNodes);
        var edges = parentEdges.ToList();
        var edgeKeys = edges
            .Select(EdgeKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var branch in branches)
            MergeBranch(nodeById, edges, edgeKeys, branch);

        var root = new JsonObject
        {
            ["nodes"] = new JsonArray(nodeById.Values.Select(n => n.DeepClone()).ToArray()),
            ["edges"] = new JsonArray(edges.Select(e => (JsonNode?)e.DeepClone()).ToArray())
        };
        return root.ToJsonString(JsonSerializerProfiles.CamelCase);
    }

    /// <summary>
    /// 親グラフ上で、指定 Fork 到達に対応する Join ノード ID を解決する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="forkNodeId"/> は並列エピソードの相関キー（親グラフの Fork 到達 <c>nodeId</c>）。
    /// 循環で同名 <paramref name="joinState"/> が複数あるとき、
    /// Fork の <c>completedAt</c>（なければ <c>startedAt</c>）以降で最も早い Join を選ぶ。
    /// 時刻が欠ける場合は、同名 Fork 訪問の出現順と Join 訪問の出現順をインデックス対応する。
    /// </para>
    /// </remarks>
    /// <param name="parentGraphJson">親の永続 graph JSON。</param>
    /// <param name="forkNodeId">並列エピソードの相関キー（親上の Fork 到達 <c>nodeId</c>）。</param>
    /// <param name="joinState">定義上の Join 状態名。</param>
    /// <returns>対応する Join の <c>nodeId</c>。未解決なら <see langword="null"/>。</returns>
    public static string? ResolveJoinNodeId(
        string parentGraphJson,
        string forkNodeId,
        string joinState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentGraphJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(forkNodeId);
        if (string.IsNullOrWhiteSpace(joinState))
            return null;

        if (!TryParseGraph(parentGraphJson, out var parentNodes, out _))
            return null;

        var nodeById = IndexNodesById(parentNodes);
        if (!nodeById.TryGetValue(forkNodeId, out var forkNode))
            return null;

        var joinCandidates = CollectJoinCandidates(nodeById.Values, joinState);
        if (joinCandidates.Count == 0)
            return null;

        var afterFork = FindJoinAfterFork(joinCandidates, ReadNodeTime(forkNode));
        if (afterFork is not null)
            return afterFork;

        return ResolveJoinByVisitIndex(nodeById, forkNode, forkNodeId, joinCandidates);
    }

    /// <summary>1 分岐分の子グラフを親へマージし、Fork / Join（または Next）辺を張る。</summary>
    private static void MergeBranch(
        Dictionary<string, JsonObject> nodeById,
        List<JsonObject> edges,
        HashSet<string> edgeKeys,
        BranchGraph branch)
    {
        if (string.IsNullOrWhiteSpace(branch.ChildGraphJson))
            return;
        if (!TryParseGraph(branch.ChildGraphJson, out var childNodes, out var childEdges))
            return;

        MergeChildNodes(nodeById, childNodes);
        MergeChildEdges(edges, edgeKeys, childEdges);

        var childNodeIds = CollectNodeIds(childNodes);
        var childTargets = CollectEdgeEndpointIds(childEdges, "to");
        var childSources = CollectEdgeEndpointIds(childEdges, "from");
        var attachmentRoots = ResolveBranchAttachmentRoots(
            childNodes,
            childNodeIds,
            childTargets,
            branch.BranchState);
        var terminals = childNodeIds.Where(id => !childSources.Contains(id)).ToList();

        AttachForkEdges(nodeById, edges, edgeKeys, branch.ForkNodeId, attachmentRoots);
        AttachJoinOrNextEdges(nodeById, edges, edgeKeys, branch, terminals);
    }

    private static void MergeChildNodes(
        Dictionary<string, JsonObject> nodeById,
        IReadOnlyList<JsonObject> childNodes)
    {
        foreach (var childNode in childNodes)
        {
            var nodeId = ReadString(childNode, NodeIdProperty);
            if (string.IsNullOrWhiteSpace(nodeId) || nodeById.ContainsKey(nodeId))
                continue;
            nodeById[nodeId] = childNode.DeepClone().AsObject();
        }
    }

    private static void MergeChildEdges(
        List<JsonObject> edges,
        HashSet<string> edgeKeys,
        IReadOnlyList<JsonObject> childEdges)
    {
        foreach (var childEdge in childEdges)
        {
            var key = EdgeKey(childEdge);
            if (!edgeKeys.Add(key))
                continue;
            edges.Add(childEdge.DeepClone().AsObject());
        }
    }

    private static void AttachForkEdges(
        Dictionary<string, JsonObject> nodeById,
        List<JsonObject> edges,
        HashSet<string> edgeKeys,
        string forkNodeId,
        IReadOnlyList<string> attachmentRoots)
    {
        if (!nodeById.ContainsKey(forkNodeId))
            return;

        foreach (var rootId in attachmentRoots)
            TryAddEdge(edges, edgeKeys, forkNodeId, rootId, (int)EdgeType.Fork);
    }

    private static void AttachJoinOrNextEdges(
        Dictionary<string, JsonObject> nodeById,
        List<JsonObject> edges,
        HashSet<string> edgeKeys,
        BranchGraph branch,
        IReadOnlyList<string> terminals)
    {
        if (string.IsNullOrWhiteSpace(branch.JoinNodeId)
            || !nodeById.ContainsKey(branch.JoinNodeId))
            return;

        foreach (var terminalId in terminals)
        {
            if (!nodeById.TryGetValue(terminalId, out var terminalNode))
                continue;

            // ネスト枝先頭の Fork 自体を親 Join へ繋がない（Join.all 依存の見た目の幽霊枝）。
            if (string.Equals(
                    ReadString(terminalNode, NodeTypeProperty),
                    "Fork",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var terminalState = ReadString(terminalNode, NodeNameProperty);
            // 枝先頭状態の終端だけ Join 辺。ネスト内側 Join などは Next（定義の next に相当）。
            var edgeType = IsBranchHeadState(terminalState, branch.BranchState)
                ? EdgeType.Join
                : EdgeType.Next;
            TryAddEdge(
                edges,
                edgeKeys,
                terminalId,
                branch.JoinNodeId,
                (int)edgeType);
        }
    }

    /// <summary>
    /// 親 Fork から繋ぐ入口。定義の <paramref name="branchState"/> ノードがあればそれだけを使う。
    /// </summary>
    private static List<string> ResolveBranchAttachmentRoots(
        IReadOnlyList<JsonObject> childNodes,
        HashSet<string> childNodeIds,
        HashSet<string> childTargets,
        string branchState)
    {
        if (!string.IsNullOrWhiteSpace(branchState))
        {
            var branchHeadIds = childNodes
                .Where(n =>
                {
                    var id = ReadString(n, NodeIdProperty);
                    var state = ReadString(n, NodeNameProperty);
                    return !string.IsNullOrWhiteSpace(id)
                        && childNodeIds.Contains(id)
                        && IsBranchHeadState(state, branchState);
                })
                .Select(n => ReadString(n, NodeIdProperty)!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (branchHeadIds.Count > 0)
                return branchHeadIds;
        }

        return childNodeIds.Where(id => !childTargets.Contains(id)).ToList();
    }

    private static Dictionary<string, JsonObject> IndexNodesById(IEnumerable<JsonObject> nodes) =>
        nodes
            .Where(n => ReadString(n, NodeIdProperty) is { Length: > 0 })
            .ToDictionary(n => ReadString(n, NodeIdProperty)!, StringComparer.Ordinal);

    private static HashSet<string> CollectNodeIds(IEnumerable<JsonObject> nodes) =>
        nodes
            .Select(n => ReadString(n, NodeIdProperty))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> CollectEdgeEndpointIds(
        IEnumerable<JsonObject> edges,
        string propertyName) =>
        edges
            .Select(e => ReadString(e, propertyName))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static List<JoinCandidate> CollectJoinCandidates(
        IEnumerable<JsonObject> nodes,
        string joinState) =>
        nodes
            .Select(n => new JoinCandidate(
                ReadString(n, NodeIdProperty),
                ReadNodeTime(n),
                string.Equals(
                    ReadString(n, NodeTypeProperty),
                    JoinNodeType,
                    StringComparison.OrdinalIgnoreCase),
                ReadString(n, NodeNameProperty)))
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Id)
                && string.Equals(x.NodeName, joinState, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.IsJoin)
            .ThenBy(x => x.Time ?? DateTime.MaxValue)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

    private static string? FindJoinAfterFork(
        IReadOnlyList<JoinCandidate> joinCandidates,
        DateTime? forkTime)
    {
        if (forkTime is null)
            return null;

        var afterFork = joinCandidates
            .Where(x => x.Time is null || x.Time >= forkTime)
            .OrderBy(x => x.Time ?? DateTime.MaxValue)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
        return afterFork.Count > 0 ? afterFork[0].Id : null;
    }

    private static string? ResolveJoinByVisitIndex(
        Dictionary<string, JsonObject> nodeById,
        JsonObject forkNode,
        string forkNodeId,
        IReadOnlyList<JoinCandidate> joinCandidates)
    {
        var forkStateName = ReadString(forkNode, NodeNameProperty);
        if (string.IsNullOrWhiteSpace(forkStateName))
            return joinCandidates[0].Id;

        var forkVisits = nodeById.Values
            .Select(n => new
            {
                Id = ReadString(n, NodeIdProperty),
                Time = ReadNodeTime(n),
                NodeName = ReadString(n, NodeNameProperty)
            })
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Id)
                && string.Equals(x.NodeName, forkStateName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Time ?? DateTime.MaxValue)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.Id!)
            .ToList();

        var joinVisits = joinCandidates
            .OrderBy(x => x.Time ?? DateTime.MaxValue)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.Id!)
            .ToList();

        var forkIndex = forkVisits.FindIndex(id => string.Equals(id, forkNodeId, StringComparison.Ordinal));
        if (forkIndex < 0 || forkIndex >= joinVisits.Count)
            return null;

        return joinVisits[forkIndex];
    }

    private static bool IsBranchHeadState(string? stateName, string branchState) =>
        !string.IsNullOrWhiteSpace(stateName)
        && !string.IsNullOrWhiteSpace(branchState)
        && string.Equals(stateName, branchState, StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonObject node, string propertyName) =>
        node[propertyName]?.GetValue<string>();

    private static DateTime? ReadNodeTime(JsonObject node)
    {
        if (TryReadTime(node, "completedAt", out var completedAt))
            return completedAt;
        if (TryReadTime(node, "startedAt", out var startedAt))
            return startedAt;
        return null;
    }

    private static bool TryReadTime(JsonObject node, string propertyName, out DateTime value)
    {
        value = default;
        var raw = ReadString(node, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
    }

    private static void TryAddEdge(
        List<JsonObject> edges,
        HashSet<string> edgeKeys,
        string from,
        string to,
        int type)
    {
        var edge = new JsonObject
        {
            ["from"] = from,
            ["to"] = to,
            ["type"] = type
        };
        var key = EdgeKey(edge);
        if (!edgeKeys.Add(key))
            return;
        edges.Add(edge);
    }

    private static string EdgeKey(JsonObject edge)
    {
        var from = ReadString(edge, "from") ?? string.Empty;
        var to = ReadString(edge, "to") ?? string.Empty;
        var type = edge["type"]?.ToJsonString() ?? string.Empty;
        return $"{from}\u001f{to}\u001f{type}";
    }

    private static bool TryParseGraph(
        string graphJson,
        out List<JsonObject> nodes,
        out List<JsonObject> edges)
    {
        nodes = [];
        edges = [];
        try
        {
            var root = JsonNode.Parse(graphJson) as JsonObject;
            if (root is null)
                return false;

            if (root["nodes"] is JsonArray nodeArray)
            {
                nodes = nodeArray
                    .OfType<JsonObject>()
                    .Select(n => n.DeepClone().AsObject())
                    .ToList();
            }

            if (root["edges"] is JsonArray edgeArray)
            {
                edges = edgeArray
                    .OfType<JsonObject>()
                    .Select(e => e.DeepClone().AsObject())
                    .ToList();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record JoinCandidate(
        string? Id,
        DateTime? Time,
        bool IsJoin,
        string? NodeName);
}
