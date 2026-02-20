namespace Statevia.Core.FSM;

/// <summary>
/// 事実駆動 FSM で使用する標準事実名。
/// 状態実行結果や Join 完了時に遷移テーブルを評価する際のキーとなります。
/// </summary>
public static class Fact
{
    /// <summary>状態が正常完了したことを示す事実。</summary>
    public const string Completed = "Completed";
    /// <summary>状態が例外で失敗したことを示す事実。</summary>
    public const string Failed = "Failed";
    /// <summary>協調的キャンセルで中止したことを示す事実。</summary>
    public const string Cancelled = "Cancelled";
    /// <summary>Join の allOf が揃い、Join 状態が実行可能になったことを示す事実。</summary>
    public const string Joined = "Joined";
}
