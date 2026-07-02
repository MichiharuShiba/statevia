namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_graph_snapshots テーブル（projection）。</summary>
public class ExecutionGraphSnapshotRow
{
    public Guid ExecutionId { get; set; }
    public required string GraphJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}
