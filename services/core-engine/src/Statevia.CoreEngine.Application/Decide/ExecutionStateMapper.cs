using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Node;

namespace Statevia.CoreEngine.Application.Decide;

/// <summary>ExecutionStateDto を Domain ExecutionState にマップする。status 文字列を enum にパースする。</summary>
public static class ExecutionStateMapper
{
    /// <summary>DTO を Domain の ExecutionState に変換。不正な status は ArgumentException。</summary>
    public static ExecutionState ToDomain(this ExecutionStateDto dto)
    {
        var nodes = new Dictionary<string, NodeState>();
        foreach (var (id, n) in dto.Nodes ?? new Dictionary<string, NodeStateDto>())
            nodes[id] = n.ToDomain();
        return new ExecutionState(
            dto.ExecutionId,
            dto.GraphId,
            ParseExecutionStatus(dto.Status),
            nodes,
            dto.Version,
            dto.CancelRequestedAt,
            dto.CanceledAt,
            dto.FailedAt,
            dto.CompletedAt);
    }

    private static NodeState ToDomain(this NodeStateDto dto) =>
        new NodeState(
            dto.NodeId,
            dto.NodeType,
            ParseNodeStatus(dto.Status),
            dto.Attempt,
            dto.WorkerId,
            dto.WaitKey,
            dto.Output,
            dto.Error,
            dto.CanceledByExecution);

    private static ExecutionStatus ParseExecutionStatus(string value) => value switch
    {
        "ACTIVE" => ExecutionStatus.ACTIVE,
        "COMPLETED" => ExecutionStatus.COMPLETED,
        "FAILED" => ExecutionStatus.FAILED,
        "CANCELED" => ExecutionStatus.CANCELED,
        _ => throw new ArgumentException($"Unknown ExecutionStatus: {value}", nameof(value)),
    };

    private static NodeStatus ParseNodeStatus(string value) => value switch
    {
        "IDLE" => NodeStatus.IDLE,
        "READY" => NodeStatus.READY,
        "RUNNING" => NodeStatus.RUNNING,
        "WAITING" => NodeStatus.WAITING,
        "SUCCEEDED" => NodeStatus.SUCCEEDED,
        "FAILED" => NodeStatus.FAILED,
        "CANCELED" => NodeStatus.CANCELED,
        _ => throw new ArgumentException($"Unknown NodeStatus: {value}", nameof(value)),
    };
}
