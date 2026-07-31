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
/// RDB / ドキュメント DB いずれでも同じ論理形状で扱えるよう、結合や一覧照会前提の
/// フィールドは持たない。現行 Postgres アダプタではテーブル
/// <c>execution_runtime_checkpoints</c> にマップする。
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
}
