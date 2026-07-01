namespace Statevia.Service.Api.Persistence;

/// <summary>
/// <see cref="Repositories.EventDeliveryDedupRepository.TryUpdateStatusAsync"/> 用の更新内容。
/// </summary>
internal sealed record EventDeliveryDedupStatusUpdate(
    string Status,
    DateTime UtcNow,
    DateTime? AppliedAt,
    string? ErrorCode);
