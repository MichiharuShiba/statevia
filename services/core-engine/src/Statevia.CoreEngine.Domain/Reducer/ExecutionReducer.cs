using Statevia.CoreEngine.Domain.Events;
using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Extensions;
using Statevia.CoreEngine.Domain.Node;

namespace Statevia.CoreEngine.Domain.Reducer;

/// <summary>
/// イベントを適用して ExecutionState を更新する純粋 Reducer。Cancel wins + normalize。
/// core-reducer-spec / core-api reducer に準拠。
/// </summary>
public static class ExecutionReducer
{
    /// <summary>ExecutionStatus の優先度（大きいほど強い）。Cancel wins の根拠。</summary>
    private static readonly Dictionary<ExecutionStatus, int> ExecRank = new()
    {
        [ExecutionStatus.CANCELED] = 400,
        [ExecutionStatus.FAILED] = 300,
        [ExecutionStatus.COMPLETED] = 200,
        [ExecutionStatus.ACTIVE] = 100,
    };

    /// <summary>NodeStatus の優先度（大きいほど強い）。WAITING→RUNNING は chooseNodeStatus で例外許可。</summary>
    private static readonly Dictionary<NodeStatus, int> NodeRank = new()
    {
        [NodeStatus.CANCELED] = 700,
        [NodeStatus.FAILED] = 600,
        [NodeStatus.SUCCEEDED] = 500,
        [NodeStatus.WAITING] = 400,
        [NodeStatus.RUNNING] = 300,
        [NodeStatus.READY] = 200,
        [NodeStatus.IDLE] = 100,
    };

    /// <summary>state に event を適用した新しい ExecutionState を返す。</summary>
    /// <param name="state">現在の状態。</param>
    /// <param name="envelope">適用するイベント。Payload は IReadOnlyDictionary&lt;string, object?&gt; として読み取る（null の場合は空として扱う）。</param>
    public static ExecutionState Reduce(ExecutionState state, EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion is not EventEnvelope.SupportedSchemaVersion)
            return state;

        if (ShouldIgnoreProgressEvent(state, envelope.Type))
            return state;

        var payload = envelope.Payload.AsPayloadDictionary();
        var s = ApplyEvent(state, envelope, payload);
        return Normalize(s);
    }

    /// <summary>ExecutionStatus の優先度値を返す。未定義は 0。</summary>
    private static int ExecRankOf(ExecutionStatus s) => ExecRank.GetValueOrDefault(s, 0);

    /// <summary>NodeStatus の優先度値を返す。未定義は 0。</summary>
    private static int NodeRankOf(NodeStatus s) => NodeRank.GetValueOrDefault(s, 0);

    /// <summary>優先度の高い方を採用（Cancel wins）。同じなら current を返す。</summary>
    private static ExecutionStatus ChooseExecStatus(ExecutionStatus current, ExecutionStatus candidate) =>
        ExecRankOf(candidate) > ExecRankOf(current) ? candidate : current;

    /// <summary>優先度の高い方を採用。WAITING→RUNNING のみ例外で常に RUNNING を返す（Resume 許可）。</summary>
    private static NodeStatus ChooseNodeStatus(NodeStatus current, NodeStatus candidate)
    {
        if ((current, candidate) is (NodeStatus.WAITING, NodeStatus.RUNNING))
            return NodeStatus.RUNNING;
        return NodeRankOf(candidate) > NodeRankOf(current) ? candidate : current;
    }

    /// <summary>Cancel 要求後に進行系イベントなら true（適用をスキップする）。core-reducer-spec §4.2。</summary>
    private static bool ShouldIgnoreProgressEvent(ExecutionState state, string type)
    {
        if (!state.IsCancelRequested()) return false;
        return type is EventTypeConstants.NodeReady or EventTypeConstants.NodeStarted
            or EventTypeConstants.NodeProgressReported or EventTypeConstants.NodeWaiting
            or EventTypeConstants.NodeResumeRequested or EventTypeConstants.NodeResumed
            or EventTypeConstants.JoinPassed or EventTypeConstants.JoinGateUpdated
            or EventTypeConstants.ForkOpened or EventTypeConstants.ExecutionCompleted
            or EventTypeConstants.ExecutionFailed;
    }

    /// <summary>指定ノードの Status を candidate で更新（chooseNodeStatus 適用）。ノードが存在しなければ state をそのまま返す。</summary>
    private static ExecutionState UpdateNodeStatus(ExecutionState state, string nodeId, NodeStatus candidate) =>
        state.WithNodeUpdated(nodeId, n => n with { Status = ChooseNodeStatus(n.Status, candidate) });

    /// <summary>Execution が CANCELED のとき、未終端ノード（IDLE/READY/RUNNING/WAITING）を CANCELED にし、canceledByExecution を true にする。</summary>
    private static ExecutionState Normalize(ExecutionState state)
    {
        if (state.Status is not ExecutionStatus.CANCELED) return state;
        var nodes = new Dictionary<string, NodeState>(state.Nodes);
        foreach (var (id, n) in state.Nodes)
        {
            if (n.Status.IsActive())
                nodes[id] = n with
                {
                    Status = ChooseNodeStatus(n.Status, NodeStatus.CANCELED),
                    CanceledByExecution = true,
                };
        }
        return state with { Nodes = nodes };
    }

    /// <summary>イベント type に応じて state を 1 件分更新。core-reducer-spec §6 applyEvent に準拠。</summary>
    private static ExecutionState ApplyEvent(
        ExecutionState state,
        EventEnvelope envelope,
        IReadOnlyDictionary<string, object?> payload)
    {
        var occurredAt = envelope.OccurredAt;
        var s = state;

        switch (envelope.Type)
        {
            case EventTypeConstants.ExecutionCreated:
                s = s with
                {
                    GraphId = payload.GetString("graphId") ?? s.GraphId,
                    Status = ExecutionStatus.ACTIVE,
                };
                break;

            case EventTypeConstants.ExecutionStarted:
                s = s with { Status = ChooseExecStatus(s.Status, ExecutionStatus.ACTIVE) };
                break;

            case EventTypeConstants.ExecutionCancelRequested:
                s = s with { CancelRequestedAt = s.CancelRequestedAt ?? occurredAt };
                break;

            case EventTypeConstants.ExecutionCanceled:
                s = s with
                {
                    CanceledAt = s.CanceledAt ?? occurredAt,
                    Status = ChooseExecStatus(s.Status, ExecutionStatus.CANCELED),
                };
                break;

            case EventTypeConstants.ExecutionFailed:
                s = s with
                {
                    FailedAt = s.FailedAt ?? occurredAt,
                    Status = ChooseExecStatus(s.Status, ExecutionStatus.FAILED),
                };
                break;

            case EventTypeConstants.ExecutionCompleted:
                s = s with
                {
                    CompletedAt = s.CompletedAt ?? occurredAt,
                    Status = ChooseExecStatus(s.Status, ExecutionStatus.COMPLETED),
                };
                break;

            case EventTypeConstants.NodeCreated:
                var nodeId = payload.GetString("nodeId");
                var nodeType = payload.GetString("nodeType");
                if (nodeId is not null && !s.Nodes.ContainsKey(nodeId))
                {
                    var newNode = new NodeState(nodeId, nodeType ?? "", NodeStatus.IDLE, 0);
                    var newNodes = new Dictionary<string, NodeState>(s.Nodes) { [nodeId] = newNode };
                    s = s with { Nodes = newNodes };
                }
                break;

            case EventTypeConstants.NodeReady:
                s = UpdateNodeStatus(s, payload.GetString("nodeId") ?? "", NodeStatus.READY);
                break;

            case EventTypeConstants.NodeStarted:
                var startedNodeId = payload.GetString("nodeId");
                if (startedNodeId is not null && s.Nodes.TryGetValue(startedNodeId, out var startedNode))
                {
                    s = UpdateNodeStatus(s, startedNodeId, NodeStatus.RUNNING)
                        .WithNodeUpdated(startedNodeId, n => n with
                        {
                            Attempt = Math.Max(n.Attempt, payload.GetInt("attempt", 1)),
                            WorkerId = payload.GetString("workerId") ?? n.WorkerId,
                        });
                }
                break;

            case EventTypeConstants.NodeWaiting:
                var waitNodeId = payload.GetString("nodeId");
                if (waitNodeId is not null && s.Nodes.TryGetValue(waitNodeId, out var waitNode))
                {
                    s = UpdateNodeStatus(s, waitNodeId, NodeStatus.WAITING)
                        .WithNodeUpdated(waitNodeId, n => n with { WaitKey = payload.GetString("waitKey") ?? n.WaitKey });
                }
                break;

            case EventTypeConstants.NodeResumed:
                var resumedNodeId = payload.GetString("nodeId");
                if (resumedNodeId is not null && s.Nodes.TryGetValue(resumedNodeId, out var resumedNode)
                    && resumedNode.Status is NodeStatus.WAITING)
                    s = UpdateNodeStatus(s, resumedNodeId, NodeStatus.RUNNING);
                break;

            case EventTypeConstants.NodeSucceeded:
                var succNodeId = payload.GetString("nodeId");
                if (succNodeId is not null && s.Nodes.TryGetValue(succNodeId, out var succNode))
                {
                    s = UpdateNodeStatus(s, succNodeId, NodeStatus.SUCCEEDED)
                        .WithNodeUpdated(succNodeId, n => n with { Output = payload.GetObject("output") ?? n.Output });
                }
                break;

            case EventTypeConstants.NodeFailReported:
                var reportNodeId = payload.GetString("nodeId");
                if (reportNodeId is not null && s.Nodes.TryGetValue(reportNodeId, out var reportNode))
                    s = s.WithNodeUpdated(reportNodeId, n => n with { Error = payload.GetObject("error") ?? n.Error });
                break;

            case EventTypeConstants.NodeFailed:
                var failNodeId = payload.GetString("nodeId");
                if (failNodeId is not null && s.Nodes.TryGetValue(failNodeId, out var failNode))
                {
                    s = UpdateNodeStatus(s, failNodeId, NodeStatus.FAILED)
                        .WithNodeUpdated(failNodeId, n => n with { Error = payload.GetObject("error") ?? n.Error });
                }
                break;

            case EventTypeConstants.NodeCanceled:
                s = UpdateNodeStatus(s, payload.GetString("nodeId") ?? "", NodeStatus.CANCELED);
                break;

            default:
                break;
        }

        return s;
    }
}
