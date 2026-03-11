namespace Statevia.Core.Api.Persistence;

/// <summary>execution_graph_snapshots テーブル（projection）。</summary>
public class ExecutionGraphSnapshotRow
{
    public Guid WorkflowId { get; set; }
    public required string GraphJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}
