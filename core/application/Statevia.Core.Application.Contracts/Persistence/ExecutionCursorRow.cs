namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// execution_cursors テーブル（operational projection）。read-model の正本ではない。
/// </summary>
public class ExecutionCursorRow
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public string? CurrentNodeId { get; set; }
    public string? CurrentRuntimeId { get; set; }
    public string? CurrentWorkerId { get; set; }
    public required string State { get; set; }
    public DateTime UpdatedAt { get; set; }
}
