namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 世代一致付き runtime checkpoint upsert の入力。
/// </summary>
/// <param name="ExecutionId">実行 ID。</param>
/// <param name="OwnerGeneration">獲得済み fencing 世代。</param>
/// <param name="CheckpointJson">runtime JSON。</param>
/// <param name="SchemaVersion">文書スキーマ版。</param>
/// <param name="UpdatedAtUtc">更新時刻 UTC。</param>
/// <param name="LeaseUntilUtc">任意の lease 延長期限。null なら lease を変更しない。</param>
public sealed record ExecutionCheckpointRuntimeUpsert(
    Guid ExecutionId,
    long OwnerGeneration,
    string CheckpointJson,
    int SchemaVersion,
    DateTime UpdatedAtUtc,
    DateTime? LeaseUntilUtc = null);
