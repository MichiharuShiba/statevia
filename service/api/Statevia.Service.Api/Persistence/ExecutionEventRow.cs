namespace Statevia.Service.Api.Persistence;

/// <summary>execution_events テーブル（監査専用）。行の PK は EventStore の event_id とは別（監査レコードの一意 ID）。</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class ExecutionEventRow
{
    public Guid ExecutionEventId { get; set; }
    public Guid ExecutionId { get; set; }
    public long Seq { get; set; }
    public required string Type { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
