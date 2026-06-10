namespace Statevia.Core.Api.Application.Actions;

/// <summary>組み込みおよび既定で解決するアクション ID。</summary>
internal static class WellKnownActionIds
{
    /// <summary>builtin canonical ID のプレフィックス。</summary>
    public const string BuiltinPrefix = "statevia.action.builtin.";

    /// <summary>legacy Registry キー（後方互換）。</summary>
    public const string NoOp = "noop";

    /// <summary>implicit noop の canonical ID。</summary>
    public const string NoOpCanonical = BuiltinPrefix + "noop";

    /// <summary>定義検証・UI デモ用の legacy 遅延アクション（フェーズ B で削除予定）。</summary>
    public const string Delay5s = "delay5s";

    private static readonly HashSet<string> s_builtinShortNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "noop",
        "delay5s",
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

        if (string.Equals(trimmed, Delay5s, StringComparison.OrdinalIgnoreCase))
        {
            return Delay5s;
        }

#pragma warning disable CA1308 // builtin 短名は YAML 小文字キーワードとして canonical 化する
        return BuiltinPrefix + trimmed.ToLowerInvariant();
#pragma warning restore CA1308
    }
}
