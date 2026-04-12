namespace Statevia.Core.Api.Persistence;

/// <summary>event_delivery_dedup テーブル（イベント配送の冪等制御）。</summary>
public class EventDeliveryDedupRow
{
    public required string TenantId { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid ClientEventId { get; set; }
    public Guid? BatchId { get; set; }
    public required string Status { get; set; }
    public DateTime AcceptedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime UpdatedAt { get; set; }
}

