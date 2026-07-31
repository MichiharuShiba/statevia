using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;

namespace Statevia.Core.Engine.ExecutionGraphs;

/// <summary>
/// ワークフローインスタンスの実行グラフ。ノードと辺で状態実行の流れを保持します。
/// 観測・デバッグ・可視化用であり、実行には影響しません。
/// </summary>
public sealed class ExecutionGraph
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly List<ExecutionNode> _nodes = [];
    private readonly List<ExecutionEdge> _edges = [];
    private readonly object _lock = new();

    /// <summary>ノード一覧のスナップショットを返します。</summary>
    public IReadOnlyList<ExecutionNode> GetNodesSnapshot()
    {
        lock (_lock)
        {
            return _nodes.ToList();
        }
    }

    /// <summary>辺一覧のスナップショットを返します。</summary>
    public IReadOnlyList<ExecutionEdge> GetEdgesSnapshot()
    {
        lock (_lock)
        {
            return _edges.ToList();
        }
    }

    /// <summary>ノードを追加し、ノード ID を返します。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="nodeType">ノード種別。</param>
    /// <param name="input">状態入力。</param>
    /// <param name="attempt">試行回数。</param>
    /// <param name="workerId">ワーカー識別子。</param>
    /// <param name="waitKey">単一イベント Wait の互換キー。</param>
    /// <param name="allowedEvents">Wait の許可イベント名一覧。</param>
    /// <returns>採番されたノード ID。</returns>
    public string AddNode(
        string stateName,
        string nodeType = "Task",
        object? input = null,
        int attempt = 1,
        string? workerId = null,
        string? waitKey = null,
        IReadOnlyList<string>? allowedEvents = null)
    {
        var nodeId = Guid.NewGuid().ToString("N")[..8];
        lock (_lock)
        {
            _nodes.Add(new ExecutionNode
            {
                NodeId = nodeId,
                StateName = stateName,
                NodeType = nodeType,
                StartedAt = DateTime.UtcNow,
                Input = input,
                Attempt = attempt,
                WorkerId = workerId ?? nodeId,
                WaitKey = waitKey,
                AllowedEvents = allowedEvents
            });
        }
        return nodeId;
    }

    /// <summary>ノードを完了としてマークし、事実と出力を記録します。</summary>
    public void CompleteNode(string nodeId, string fact, object? output)
    {
        lock (_lock)
        {
            var node = _nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node != null)
            {
                node.CompletedAt = DateTime.UtcNow;
                node.Fact = fact;
                node.Output = output;
                node.CanceledByExecution = string.Equals(fact, "Cancelled", StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// ノードに output 条件遷移の診断を付与する（同一ノードへの再呼び出しで上書き可能）。
    /// </summary>
    public void SetNodeConditionRouting(string nodeId, ConditionRoutingDiagnostics? diagnostics)
    {
        lock (_lock)
        {
            var node = _nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (node is not null)
            {
                node.ConditionRouting = diagnostics;
            }
        }
    }

    /// <summary>実行グラフに辺を追加する。</summary>
    /// <param name="fromNodeId">始点ノード ID。</param>
    /// <param name="toNodeId">終点ノード ID。</param>
    /// <param name="type">辺の種類。</param>
    public void AddEdge(string fromNodeId, string toNodeId, EdgeType type)
    {
        lock (_lock) { _edges.Add(new ExecutionEdge { From = fromNodeId, To = toNodeId, Type = type }); }
    }

    /// <summary>
    /// チェックポイントからグラフを復元する（既存内容は破棄し、ノード ID を維持する）。
    /// </summary>
    /// <param name="data">グラフ断面。</param>
    public void ImportFromCheckpoint(CheckpointGraphData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_lock)
        {
            _nodes.Clear();
            _edges.Clear();
            foreach (var node in data.Nodes)
            {
                _nodes.Add(new ExecutionNode
                {
                    NodeId = node.NodeId,
                    StateName = node.StateName,
                    NodeType = node.NodeType,
                    StartedAt = node.StartedAt,
                    CompletedAt = node.CompletedAt,
                    Fact = node.Fact,
                    Output = CheckpointJson.FromElement(node.Output),
                    Input = CheckpointJson.FromElement(node.Input),
                    Attempt = node.Attempt,
                    WorkerId = node.WorkerId,
                    WaitKey = node.WaitKey,
                    AllowedEvents = node.AllowedEvents,
                    CanceledByExecution = node.CanceledByExecution
                });
            }

            foreach (var edge in data.Edges)
            {
                if (!Enum.TryParse<EdgeType>(edge.Type, ignoreCase: true, out var edgeType))
                {
                    edgeType = EdgeType.Next;
                }

                _edges.Add(new ExecutionEdge { From = edge.From, To = edge.To, Type = edgeType });
            }
        }
    }

    /// <summary>チェックポイント用にグラフ断面をエクスポートする。</summary>
    public CheckpointGraphData ExportCheckpoint()
    {
        lock (_lock)
        {
            return new CheckpointGraphData
            {
                Nodes = _nodes.Select(n => new CheckpointGraphNode
                {
                    NodeId = n.NodeId,
                    StateName = n.StateName,
                    NodeType = n.NodeType,
                    StartedAt = n.StartedAt,
                    CompletedAt = n.CompletedAt,
                    Fact = n.Fact,
                    Output = CheckpointJson.ToElement(n.Output),
                    Input = CheckpointJson.ToElement(n.Input),
                    Attempt = n.Attempt,
                    WorkerId = n.WorkerId,
                    WaitKey = n.WaitKey,
                    AllowedEvents = n.AllowedEvents,
                    CanceledByExecution = n.CanceledByExecution
                }).ToList(),
                Edges = _edges.Select(e => new CheckpointGraphEdge
                {
                    From = e.From,
                    To = e.To,
                    Type = e.Type.ToString()
                }).ToList()
            };
        }
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
