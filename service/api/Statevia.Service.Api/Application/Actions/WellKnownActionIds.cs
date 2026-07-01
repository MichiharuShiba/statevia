namespace Statevia.Service.Api.Application.Actions;

/// <summary>builtin action の canonical ID 定数と短名解決。</summary>
internal static class WellKnownActionIds
{
    /// <summary>builtin canonical ID のプレフィックス。</summary>
    public const string BuiltinPrefix = "statevia.action.builtin.";

    /// <summary>legacy Registry キー（noop エイリアス）。</summary>
    public const string NoOp = "noop";

    /// <summary>implicit noop の canonical ID。</summary>
    public const string NoOpCanonical = BuiltinPrefix + "noop";

    /// <summary>時間待機 builtin。</summary>
    public const string Sleep = BuiltinPrefix + "sleep";

    /// <summary>HTTP 呼び出し builtin。</summary>
    public const string Rest = BuiltinPrefix + "rest";

    /// <summary>通知送信 builtin。</summary>
    public const string Notify = BuiltinPrefix + "notify";

    /// <summary>実行スコープ内シグナル builtin。</summary>
    public const string Signal = BuiltinPrefix + "signal";

    /// <summary>システムイベント発行 builtin。</summary>
    public const string Publish = BuiltinPrefix + "publish";

    /// <summary>子ワークフロー起動 builtin。</summary>
    public const string Workflow = BuiltinPrefix + "workflow";

    private static readonly HashSet<string> s_builtinShortNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "noop",
        "sleep",
        "rest",
        "notify",
        "signal",
        "publish",
        "workflow",
    };

    /// <summary>builtin 短名かどうかを判定する。</summary>
    /// <param name="actionRef">YAML 上の action 参照。</param>
    public static bool IsBuiltinShortName(string actionRef) =>
        !string.IsNullOrWhiteSpace(actionRef) && s_builtinShortNames.Contains(actionRef.Trim());

    /// <summary>builtin 短名を canonical ID に変換する。</summary>
    /// <param name="shortName">builtin 短名。</param>
    /// <returns>canonical action ID。</returns>
    public static string ToCanonicalActionId(string shortName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortName);
        var trimmed = shortName.Trim();

#pragma warning disable CA1308 // builtin 短名は YAML 小文字キーワードとして canonical 化する
        return BuiltinPrefix + trimmed.ToLowerInvariant();
#pragma warning restore CA1308
    }
}
