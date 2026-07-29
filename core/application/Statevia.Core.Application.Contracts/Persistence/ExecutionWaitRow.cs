namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>execution_waits.wait_kind</c>。仕様で列挙した durable wait のみ DB に永続化する。
/// </summary>
public enum ExecutionWaitKind
{
    /// <summary>名前付きイベント待ち（Engine Wait ノード: WaitEventRouteTable + WaitAsync / Resume）。</summary>
    EventWait,

    /// <summary>外部コールバック待ち（将来拡張）。</summary>
    CallbackWait,

    /// <summary>長時間遅延待ち（将来拡張）。</summary>
    DelayWait
}

/// <summary>execution_waits テーブル。durable wait のみ永続化する（1 Wait ノード = 1 行）。</summary>
/// <remarks>
/// <para><c>allowed_events</c> は受付可能なイベント名配列。Resume 時の行削除キーは <see cref="NodeId"/>。</para>
/// </remarks>
public class ExecutionWaitRow
{
    /// <summary>実行インスタンス ID。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>待機中 Wait ノード ID（PK の一部）。</summary>
    public required string NodeId { get; set; }

    /// <summary>wait 種別。</summary>
    public ExecutionWaitKind WaitKind { get; set; }

    /// <summary>許可イベント名一覧（JSONB）。</summary>
    public required IReadOnlyList<string> AllowedEvents { get; set; }

    /// <summary>有効期限（EventWait は null）。</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>行作成時刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
