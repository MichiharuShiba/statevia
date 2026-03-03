namespace Statevia.CoreEngine.Domain.Extensions;

/// <summary>イベント Payload 辞書（IReadOnlyDictionary&lt;string, object?&gt;）の拡張メソッド。</summary>
public static class PayloadDictionaryExtensions
{
    /// <summary>object? を Payload 辞書として取得。null または非辞書の場合は空辞書を返す。</summary>
    public static IReadOnlyDictionary<string, object?> AsPayloadDictionary(this object? value) =>
        value is IReadOnlyDictionary<string, object?> dict ? dict : new Dictionary<string, object?>();

    /// <summary>指定 key の文字列値を取得。無い場合・非 string は null。</summary>
    public static string? GetString(this IReadOnlyDictionary<string, object?> payload, string key)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload.TryGetValue(key, out var v) && v is string s ? s : null;
    }

    /// <summary>指定 key の整数値を取得。無い場合・変換不可は defaultValue。</summary>
    public static int GetInt(this IReadOnlyDictionary<string, object?> payload, string key, int defaultValue = 0)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!payload.TryGetValue(key, out var v) || v is null) return defaultValue;
        return v switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => int.TryParse(v.ToString(), out var parsed) ? parsed : defaultValue,
        };
    }

    /// <summary>指定 key の値をそのまま取得。無い場合は null。</summary>
    public static object? GetObject(this IReadOnlyDictionary<string, object?> payload, string key)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload.TryGetValue(key, out var v) ? v : null;
    }
}
