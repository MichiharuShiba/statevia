namespace Statevia.Core.Api.Persistence;

/// <summary>
/// <c>execution_waits.wait_kind</c>。仕様で列挙した durable wait のみ DB に永続化する。
/// </summary>
internal enum ExecutionWaitKind
{
    /// <summary>名前付きイベント待ち（Engine Wait ノード: WaitTable + WaitAsync + Publish）。</summary>
    EventWait,

    /// <summary>外部コールバック待ち（将来拡張）。</summary>
    CallbackWait,

    /// <summary>長時間遅延待ち（将来拡張）。</summary>
    DelayWait
}
