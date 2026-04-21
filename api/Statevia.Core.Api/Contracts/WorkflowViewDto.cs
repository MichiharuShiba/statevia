using System.Text.Json;

namespace Statevia.Core.Api.Contracts;

/// <summary>UI <c>WorkflowView</c> に近い形（camelCase JSON）。GET …/state 等で返す。</summary>
public sealed class WorkflowViewDto
{
    public string DisplayId { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string GraphId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool CancelRequested { get; init; }
    public bool RestartLost { get; init; }
    public IReadOnlyList<WorkflowViewNodeDto> Nodes { get; init; } = Array.Empty<WorkflowViewNodeDto>();
}

public sealed class WorkflowViewNodeDto
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Attempt { get; init; }
    public string? WorkerId { get; init; }
    public string? WaitKey { get; init; }
    public bool CanceledByExecution { get; init; }
    /// <summary>条件遷移の評価情報（ExecutionGraph の conditionRouting をそのまま透過）。</summary>
    public JsonElement? ConditionRouting { get; init; }
}

/// <summary>GET …/events のレスポンス（UI <c>ExecutionEventsResponse</c>）。</summary>
public sealed class ExecutionEventsResponseDto
{
    public IReadOnlyList<TimelineEventDto> Events { get; init; } = Array.Empty<TimelineEventDto>();
    public bool HasMore { get; init; }
}

/// <summary>タイムライン／SSE 用のイベント（UI の <c>ExecutionStreamEvent</c> + seq）。</summary>
public sealed class TimelineEventDto
{
    public long Seq { get; init; }
    public string Type { get; init; } = string.Empty;
    public string ExecutionId { get; init; } = string.Empty;
    public string? To { get; init; }
    public string? From { get; init; }
    public GraphUpdatedPatchDto? Patch { get; init; }
    public string? At { get; init; }
}

public sealed class GraphUpdatedPatchDto
{
    public IReadOnlyList<GraphPatchNodeDto>? Nodes { get; init; }
}

public sealed class GraphPatchNodeDto
{
    public string NodeId { get; init; } = string.Empty;
    public string? Status { get; init; }
    public int? Attempt { get; init; }
    public string? WaitKey { get; init; }
    public bool? CanceledByExecution { get; init; }
}

public sealed class ResumeNodeRequest
{
    /// <summary>Wait を再開するイベント名（Engine.PublishEvent に渡す）。</summary>
    public string? ResumeKey { get; init; }
}
