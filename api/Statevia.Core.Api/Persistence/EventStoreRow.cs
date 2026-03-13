namespace Statevia.Core.Api.Persistence;

/// <summary>event_store テーブル（イベントソース専用・U2 案 A）。</summary>
public class EventStoreRow
{
    public Guid EventId { get; set; }
    public Guid WorkflowId { get; set; }
    public long Seq { get; set; }
    public required string Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? ActorKind { get; set; }
    public string? ActorId { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public int SchemaVersion { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
