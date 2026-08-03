namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>永続ワークキューの処理種別。</summary>
public static class ExecutionWorkItemKinds
{
    /// <summary>実行開始。</summary>
    public const string Start = "Start";

    /// <summary>Wait ノードの再開。</summary>
    public const string Resume = "Resume";

    /// <summary>実行のキャンセル。</summary>
    public const string Cancel = "Cancel";
}

/// <summary>
/// <c>execution_work_items</c> の永続ワーク項目。
/// </summary>
/// <remarks>
/// <para>複数 API プロセスが lease を取得して処理する。ペイロードは種別ごとの JSON を保持する。</para>
/// <para>完了項目は削除し、失敗時は lease を解放して再試行可能にする。</para>
/// </remarks>
public sealed class ExecutionWorkItemRow
{
    /// <summary>ワーク項目 ID（PK）。</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>対象実行 ID。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary><see cref="ExecutionWorkItemKinds"/> の処理種別。</summary>
    public required string Kind { get; set; }

    /// <summary>種別ごとの JSON ペイロード。</summary>
    public required string Payload { get; set; }

    /// <summary>処理可能となる UTC 時刻。</summary>
    public DateTime AvailableAt { get; set; }

    /// <summary>lease を取得したワーカー ID。</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>lease の UTC 有効期限。</summary>
    public DateTime? LeaseUntil { get; set; }

    /// <summary>claim 回数。</summary>
    public int Attempts { get; set; }

    /// <summary>作成 UTC 時刻。</summary>
    public DateTime CreatedAt { get; set; }
}
