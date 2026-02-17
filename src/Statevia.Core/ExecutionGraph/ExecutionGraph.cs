using System.Text.Json;

namespace Statevia.Core.ExecutionGraphs;

/// <summary>
/// ワークフローインスタンスの実行グラフ。ノードと辺で状態実行の流れを保持します。
/// 観測・デバッグ・可視化用であり、実行には影響しません。
/// </summary>
public sealed class ExecutionGraph
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly List<ExecutionNode> _nodes = [];
    private readonly List<ExecutionEdge> _edges = [];
    private readonly object _lock = new();

    /// <summary>ノード一覧のスナップショット。</summary>
    public IReadOnlyList<ExecutionNode> Nodes { get { lock (_lock) { return _nodes.ToList(); } } }
    /// <summary>辺一覧のスナップショット。</summary>
    public IReadOnlyList<ExecutionEdge> Edges { get { lock (_lock) { return _edges.ToList(); } } }

    /// <summary>ノードを追加し、ノード ID を返します。</summary>
    public string AddNode(string stateName)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        lock (_lock) { _nodes.Add(new ExecutionNode { NodeId = nodeId, StateName = stateName, StartedAt = DateTime.UtcNow }); }
        return nodeId;
    }

    /// <summary>ノードを完了としてマークし、事実と出力を記録します。</summary>
    public void CompleteNode(string nodeId, string fact, object? output)
    {
        lock (_lock)
        {
            var node = _nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node != null) { node.CompletedAt = DateTime.UtcNow; node.Fact = fact; node.Output = output; }
        }
    }

    public void AddEdge(string fromNodeId, string toNodeId, EdgeType type)
    {
        lock (_lock) { _edges.Add(new ExecutionEdge { FromNodeId = fromNodeId, ToNodeId = toNodeId, Type = type }); }
    }

    /// <summary>実行グラフを JSON としてエクスポートします。</summary>
    public string ExportJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(new { nodes = _nodes, edges = _edges }, s_jsonOptions);
        }
    }
}
