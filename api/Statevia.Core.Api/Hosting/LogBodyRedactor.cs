using System.Text;
using System.Text.RegularExpressions;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// ログ用スナップショットの簡易マスキング（STV-408 前の妥協実装）。
/// </summary>
public static class LogBodyRedactor
{
    /// <summary>クエリ名・JSON キー双方でマスク対象とする資格情報系サブ文字列（例: <c>oauth_token</c>）。</summary>
    private static readonly string[] CredentialSubstringsInNames =
        ["password", "token", "secret"];

    private static readonly string[] SensitiveJsonKeys =
    [
        ..CredentialSubstringsInNames,
        "accessToken", "refreshToken", "authorization",
        "workflowInput", "input", "output"
    ];

    /// <summary>
    /// 長さ切り詰め後、クエリ文字列と JSON 風テキストの代表キーをマスクする。
    /// </summary>
    public static string Redact(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        // ログ肥大化防止のため先に切り詰め、その上でクエリ→JSON の順にマスク
        var truncated = text.Length <= maxChars
            ? text
            : text[..maxChars] + "...[truncated]";

        var s = RedactQueryParameters(truncated);
        s = RedactJsonLikeKeys(s);
        return s;
    }

    private static string RedactQueryParameters(string query)
    {
        if (string.IsNullOrEmpty(query))
            return query;

        var trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
            return query.StartsWith("?", StringComparison.Ordinal) ? "?" : "";

        // ?a=1&b=2 形式を & で分割し、name=value の name で機微判定
        var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder("?");
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                sb.Append('&');
            var p = parts[i];
            var eq = p.IndexOf('=');
            if (eq <= 0)
            {
                sb.Append(p);
                continue;
            }

            var name = p[..eq];
            // 機微なら値を捨ててプレースホルダのみ残す
            if (IsSensitiveName(name))
                sb.Append(name).Append("=[redacted]");
            else
                sb.Append(p);
        }

        return sb.ToString();
    }

    private static bool IsSensitiveName(string name)
    {
        // oauth_token など、名前に資格情報系サブ文字列が含まれる場合
        foreach (var sub in CredentialSubstringsInNames)
        {
            if (name.Contains(sub, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // workflowInput など、サブ文字列では拾えない既知キーとの完全一致
        foreach (var k in SensitiveJsonKeys)
        {
            if (name.Equals(k, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string RedactJsonLikeKeys(string s)
    {
        // 完全な JSON パースはせず、代表的な "key": value パターンのみ置換
        foreach (var key in SensitiveJsonKeys)
        {
            // 値: ダブルクォート文字列 | 数値・true/false/null 等の非クォート（カンマ・}・空白まで）
            var pattern =
                $@"(""{Regex.Escape(key)}""\s*:\s*)(?:""[^""]*""|[^,}}\s]+)";
            s = Regex.Replace(s, pattern, "$1\"[redacted]\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return s;
    }
}
