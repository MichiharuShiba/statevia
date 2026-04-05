using Microsoft.AspNetCore.Http;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// W3C <c>traceparent</c> またはフォールバックヘッダからログ用 trace ID を決定する。
/// </summary>
public static class TraceIdResolver
{
    private const int MaxCustomHeaderLength = 128;

    /// <summary>
    /// 優先順位: <c>traceparent</c>（有効時）→ <c>X-Trace-Id</c> → <c>X-Request-Id</c> → 生成（32 hex）。
    /// </summary>
    public static string ResolveTraceId(HttpRequest request)
    {
        // 分散トレース標準: 有効な trace-id（32 hex）のみ採用
        var traceParent = request.Headers["traceparent"].FirstOrDefault();
        if (!string.IsNullOrEmpty(traceParent) && TryParseTraceParent(traceParent, out var fromParent))
            return fromParent;

        // クライアント指定の相関 ID（長さ・空白は Sanitize で弾く）
        var xt = SanitizeCustomTraceHeader(request.Headers["X-Trace-Id"].FirstOrDefault());
        if (xt != null)
            return xt;

        var xr = SanitizeCustomTraceHeader(request.Headers["X-Request-Id"].FirstOrDefault());
        if (xr != null)
            return xr;

        // 上記が無い場合はサーバ生成（ログ上の一意性用）
        return Guid.NewGuid().ToString("N");
    }

    internal static bool TryParseTraceParent(string value, out string traceId)
    {
        traceId = "";
        var parts = value.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            return false;
        // version (2 hex) - trace-id (32 hex) - parent-id (16 hex) - flags (2 hex)
        if (parts[1].Length != 32)
            return false;
        foreach (var c in parts[1])
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        traceId = parts[1];
        return true;
    }

    private static string? SanitizeCustomTraceHeader(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim();
        // 異常に長い値はヘッダインジェクション・ログ肥大化防止のため無効扱い
        if (t.Length == 0 || t.Length > MaxCustomHeaderLength)
            return null;
        return t;
    }
}
