namespace Statevia.Core.Api.Persistence;

/// <summary>execution_graph_snapshots テーブル（projection）。</summary>
internal class ExecutionGraphSnapshotRow
{
    public Guid ExecutionId { get; set; }
    public required string GraphJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}
