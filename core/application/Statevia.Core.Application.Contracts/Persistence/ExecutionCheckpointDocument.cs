namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 再開可能なランタイム状態のチェックポイント文書。
/// </summary>
/// <remarks>
/// <para>
/// キーは <see cref="ExecutionId"/>。本体は <see cref="CheckpointJson"/>（Engine の
/// <c>ExecutionRuntimeCheckpoint</c>）と <see cref="SchemaVersion"/>。
/// </para>
/// <para>
/// 実行セッション所有は <see cref="OwnerWorkerId"/> / <see cref="LeaseUntil"/> /
/// <see cref="OwnerGeneration"/>。Wait 中は所有者なし（NULL）。
/// </para>
/// </remarks>
public class ExecutionCheckpointDocument
{
    /// <summary>実行インスタンス ID（文書キー）。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>チェックポイント JSON（Engine <c>ExecutionRuntimeCheckpoint</c>）。</summary>
    public required string CheckpointJson { get; set; }

    /// <summary>文書スキーマ版（Engine checkpoint の SchemaVersion と対応）。</summary>
    public int SchemaVersion { get; set; }

    /// <summary>更新時刻 UTC。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>実行中の Worker ID。未所有・Wait 中は null。</summary>
    public string? OwnerWorkerId { get; set; }

    /// <summary>所有 lease の UTC 期限（検知用）。</summary>
    public DateTime? LeaseUntil { get; set; }

    /// <summary>fencing token。所有獲得のたびに増加する。</summary>
    public long OwnerGeneration { get; set; }
}
