namespace Statevia.CoreEngine.Application.Decide;

/// <summary>basis.execution.nodes の要素。JSON デシリアライズ用。status は文字列（例: "IDLE", "RUNNING"）。</summary>
public sealed record NodeStateDto(
    string NodeId,
    string NodeType,
    string Status,
    int Attempt,
    string? WorkerId = null,
    string? WaitKey = null,
    object? Output = null,
    object? Error = null,
    bool CanceledByExecution = false);
