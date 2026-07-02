namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>event_delivery_dedup テーブル（イベント配送の冪等制御）。</summary>
public class EventDeliveryDedupRow
{
    public required Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid ClientEventId { get; set; }
    public Guid? BatchId { get; set; }
    public required string Status { get; set; }
    public DateTime AcceptedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>event_delivery_dedup ステータス更新用 DTO。</summary>
public sealed record EventDeliveryDedupStatusUpdate(
    string Status,
    DateTime UtcNow,
    DateTime? AppliedAt,
    string? ErrorCode);
