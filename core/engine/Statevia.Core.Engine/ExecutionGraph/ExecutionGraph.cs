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
    /// <summary>
    /// 新規実行ノード ID の Hex 桁数（約 48 bit）。
    /// </summary>
    /// <remarks>
    /// 誕生日近似で n=10^4 でも衝突前確率は ~10^-7 程度。既存実行の 8 桁 ID は変換しない。
    /// </remarks>
    private const int ExecutionNodeIdHexLength = 12;

    /// <summary>
    /// 同一グラフ内で衝突したときの再採番上限。
    /// </summary>
    /// <remarks>
    /// 異常乱数源やテスト注入時に無限ループしないための安全弁。Hex 12 の正規運用では到達しない。
    /// </remarks>
    private const int ExecutionNodeIdMaxAllocationAttempts = 32;

    private readonly List<ExecutionNode> _nodes = [];
    private readonly List<ExecutionEdge> _edges = [];
    private readonly object _lock = new();
    private Func<string>? _nodeIdCandidateFactory;

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
    /// <remarks>
    /// <para>新規採番は <c>Guid("N")</c> 先頭 12 Hex。同一グラフ内で衝突したら再採番し、上限超過で失敗する。</para>
    /// <para>既存 checkpoint の 8 桁 ID はそのまま読み取り互換とする。</para>
    /// </remarks>
    /// <param name="stateName">状態名。</param>
    /// <param name="nodeType">ノード種別。</param>
    /// <param name="input">状態入力。</param>
    /// <param name="attempt">試行回数。</param>
    /// <param name="workerId">ワーカー識別子。</param>
    /// <param name="wait">Wait ノード用の観測メタデータ（任意）。</param>
    /// <returns>採番されたノード ID。</returns>
    /// <exception cref="InvalidOperationException">再採番上限内に一意な ID を割り当てられなかったとき。</exception>
    public string AddNode(
        string stateName,
        string nodeType = "Task",
        object? input = null,
        int attempt = 1,
        string? workerId = null,
        WaitNodeGraphMetadata? wait = null)
    {
        lock (_lock)
        {
            var nodeId = AllocateUniqueNodeIdUnlocked();
            _nodes.Add(new ExecutionNode
            {
                NodeId = nodeId,
                StateName = stateName,
                NodeType = nodeType,
                StartedAt = DateTime.UtcNow,
                Input = input,
                Attempt = attempt,
                WorkerId = workerId ?? nodeId,
                WaitKey = wait?.WaitKey,
                AllowedEvents = wait?.AllowedEvents,
                Subscriptions = wait?.Subscriptions
            });
            return nodeId;
        }
    }

    /// <summary>単体テスト用に nodeId 候補生成を差し替える。本番コードは呼び出さない。</summary>
    /// <param name="factory">候補生成デリゲート。null で既定（Guid 先頭 12）に戻す。</param>
    internal void SetNodeIdCandidateFactoryForTests(Func<string>? factory) =>
        _nodeIdCandidateFactory = factory;

    /// <summary>同一グラフ内で一意な実行ノード ID を割り当てる（呼び出し元が <c>_lock</c> を保持していること）。</summary>
    private string AllocateUniqueNodeIdUnlocked()
    {
        for (var attempt = 0; attempt < ExecutionNodeIdMaxAllocationAttempts; attempt++)
        {
            var candidate = CreateNodeIdCandidate();
            if (FindNodeUnlocked(candidate) is null)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Failed to allocate a unique execution node ID within {ExecutionNodeIdMaxAllocationAttempts} attempts.");
    }

    /// <summary>実行ノード ID の候補を 1 件生成する。</summary>
    private string CreateNodeIdCandidate()
    {
        if (_nodeIdCandidateFactory is not null)
        {
            return _nodeIdCandidateFactory();
        }

        return Guid.NewGuid().ToString("N")[..ExecutionNodeIdHexLength];
    }

    /// <summary>
    /// 同一グラフ内のノードを NodeId で探す（大文字小文字無視。呼び出し元が <c>_lock</c> を保持していること）。
    /// </summary>
    private ExecutionNode? FindNodeUnlocked(string nodeId) =>
        _nodes.Find(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

    /// <summary>ノードを完了としてマークし、事実と出力を記録します。</summary>
    public void CompleteNode(string nodeId, string fact, object? output)
    {
        lock (_lock)
        {
            var node = FindNodeUnlocked(nodeId);
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
            var node = FindNodeUnlocked(nodeId);
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
                    Subscriptions = MapSubscriptions(node.Subscriptions),
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
                    Subscriptions = MapCheckpointSubscriptions(n.Subscriptions),
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

    private static List<WaitSubscriptionSnapshot>? MapSubscriptions(
        IReadOnlyList<CheckpointWaitSubscription>? subscriptions) =>
        subscriptions?
            .Select(s => new WaitSubscriptionSnapshot
            {
                Topic = s.Topic,
                Key = s.Key,
                ResumeEventName = s.ResumeEventName
            })
            .ToList();

    private static List<CheckpointWaitSubscription>? MapCheckpointSubscriptions(
        IReadOnlyList<WaitSubscriptionSnapshot>? subscriptions) =>
        subscriptions?
            .Select(s => new CheckpointWaitSubscription
            {
                Topic = s.Topic,
                Key = s.Key,
                ResumeEventName = s.ResumeEventName
            })
            .ToList();
}

/// <summary>グラフノードへ載せる Wait 観測メタデータ。</summary>
/// <param name="WaitKey">単一イベント Wait の互換キー。</param>
/// <param name="AllowedEvents">Wait の許可イベント名一覧。</param>
/// <param name="Subscriptions">集合配送購読スナップショット。</param>
public sealed record WaitNodeGraphMetadata(
    string? WaitKey = null,
    IReadOnlyList<string>? AllowedEvents = null,
    IReadOnlyList<WaitSubscriptionSnapshot>? Subscriptions = null);
