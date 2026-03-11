namespace Statevia.Core.Api.Persistence;

/// <summary>workflow_events テーブル（監査専用）。行の PK は EventStore の event_id とは別（監査レコードの一意 ID）。</summary>
public class WorkflowEventRow
{
    public Guid WorkflowEventId { get; set; }
    public Guid WorkflowId { get; set; }
    public long Seq { get; set; }
    public required string Type { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
