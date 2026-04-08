using System.Text;
using System.Text.RegularExpressions;

namespace Statevia.Core.Engine.Infrastructure.Logging;

/// <summary>
/// API / Engine 共通のログ用マスキング処理を提供する。
/// </summary>
public static class LogRedaction
{
    /// <summary>クエリ名・JSON キー双方でマスク対象とする資格情報系サブ文字列。</summary>
    private static readonly string[] CredentialSubstringsInNames =
        ["password", "token", "secret"];

    /// <summary>既知の機微キー（IO-14 / STV-408）。</summary>
    private static readonly string[] SensitiveKeys =
    [
        ..CredentialSubstringsInNames,
        "accessToken", "refreshToken", "authorization",
        "workflowInput", "input", "output"
    ];

    /// <summary>
    /// 長さ切り詰め後、クエリ文字列と JSON 風テキストの代表キーをマスクする。
    /// </summary>
    /// <param name="text">ログ化対象テキスト。</param>
    /// <param name="maxChars">切り詰め上限文字数。</param>
    /// <returns>機微値を置換したテキスト。</returns>
    public static string Redact(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var truncated = text.Length <= maxChars
            ? text
            : text[..maxChars] + "...[truncated]";

        var result = RedactQueryParameters(truncated);
        result = RedactJsonLikeKeys(result);
        return result;
    }

    /// <summary>
    /// クエリ文字列の機微キーをマスクする。
    /// </summary>
    /// <param name="query">クエリ文字列。</param>
    /// <returns>マスク済みクエリ文字列。</returns>
    public static string RedactQueryParameters(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return query;
        }

        if (!query.StartsWith("?", StringComparison.Ordinal))
        {
            return query;
        }

        var trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
        {
            return query.StartsWith("?", StringComparison.Ordinal) ? "?" : "";
        }

        var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var stringBuilder = new StringBuilder("?");
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                stringBuilder.Append('&');
            }

            var pair = parts[i];
            var equalSignIndex = pair.IndexOf('=', StringComparison.Ordinal);
            if (equalSignIndex <= 0)
            {
                stringBuilder.Append(pair);
                continue;
            }

            var name = pair[..equalSignIndex];
            if (IsSensitiveName(name))
            {
                stringBuilder.Append(name).Append("=[redacted]");
            }
            else
            {
                stringBuilder.Append(pair);
            }
        }

        return stringBuilder.ToString();
    }

    private static bool IsSensitiveName(string name)
    {
        foreach (var substring in CredentialSubstringsInNames)
        {
            if (name.Contains(substring, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var key in SensitiveKeys)
        {
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RedactJsonLikeKeys(string text)
    {
        var redactedText = text;
        foreach (var key in SensitiveKeys)
        {
            var pattern =
                $@"(""{Regex.Escape(key)}""\s*:\s*)(?:""[^""]*""|[^,}}\s]+)";
            redactedText = Regex.Replace(
                redactedText,
                pattern,
                "$1\"[redacted]\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return redactedText;
    }
}
