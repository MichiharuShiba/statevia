namespace Statevia.CoreEngine.Application.Decide;

/// <summary>basis.execution のスナップショット。JSON デシリアライズ用。status は文字列（例: "ACTIVE", "CANCELED"）。</summary>
public sealed record ExecutionStateDto(
    string ExecutionId,
    string GraphId,
    string Status,
    IReadOnlyDictionary<string, NodeStateDto> Nodes,
    int Version,
    string? CancelRequestedAt = null,
    string? CanceledAt = null,
    string? FailedAt = null,
    string? CompletedAt = null);
