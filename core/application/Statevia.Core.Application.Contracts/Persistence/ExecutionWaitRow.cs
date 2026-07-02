namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>execution_waits.wait_kind</c>。仕様で列挙した durable wait のみ DB に永続化する。
/// </summary>
public enum ExecutionWaitKind
{
    /// <summary>名前付きイベント待ち（Engine Wait ノード: WaitTable + WaitAsync + Publish）。</summary>
    EventWait,

    /// <summary>外部コールバック待ち（将来拡張）。</summary>
    CallbackWait,

    /// <summary>長時間遅延待ち（将来拡張）。</summary>
    DelayWait
}

/// <summary>execution_waits テーブル。durable wait のみ永続化する。</summary>
public class ExecutionWaitRow
{
    public Guid ExecutionId { get; set; }
    public required string NodeId { get; set; }
    public ExecutionWaitKind WaitKind { get; set; }
    public required string ResumeToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
