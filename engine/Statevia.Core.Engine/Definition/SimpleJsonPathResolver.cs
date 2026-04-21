using System.Collections;
using System.Text.Json;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// SimpleJsonPath（<c>$</c> / <c>$.a.b</c>）の解決器。
/// 入力マッピング評価と条件遷移評価で共通利用する。
/// </summary>
internal static class SimpleJsonPathResolver
{
    /// <summary>path が <c>$.</c> で始まらず、フォールバック判断が必要。</summary>
    public const string IgnoredNonDollarDotPath = "IgnoredNonDollarDotPath";

    /// <summary>JSONPath 風セグメントが辞書に存在しない。</summary>
    public const string PathSegmentMissing = "PathSegmentMissing";

    /// <summary>中間値がマッピング型ではなくトラバース不能。</summary>
    public const string PathTraversalNotMapping = "PathTraversalNotMapping";

    /// <summary>SimpleJsonPath 解決結果。</summary>
    internal readonly record struct ResolveResult(
        bool IsSupportedPathExpression,
        bool Found,
        object? Value,
        string? WarningReason);

    /// <summary>
    /// path を source に対して解決する。
    /// 非対応パスや未解決時は WarningReason を返す。
    /// </summary>
    public static ResolveResult Resolve(object? source, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ResolveResult(IsSupportedPathExpression: false, Found: false, Value: source, WarningReason: null);
        }

        if (path == "$")
        {
            return new ResolveResult(IsSupportedPathExpression: true, Found: source is not null, Value: source, WarningReason: null);
        }

        if (!path.StartsWith("$.", StringComparison.Ordinal))
        {
            return new ResolveResult(
                IsSupportedPathExpression: false,
                Found: false,
                Value: source,
                WarningReason: IgnoredNonDollarDotPath);
        }

        object? current = source;
        var segments = path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (!TryGetSegmentValue(current, segment, out current, out var warningReason))
            {
                return new ResolveResult(
                    IsSupportedPathExpression: true,
                    Found: false,
                    Value: null,
                    WarningReason: warningReason);
            }
        }

        return new ResolveResult(IsSupportedPathExpression: true, Found: true, Value: current, WarningReason: null);
    }

    private static bool TryGetSegmentValue(object? current, string segment, out object? next, out string? warningReason)
    {
        if (current is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            if (!readOnlyDictionary.TryGetValue(segment, out next))
            {
                warningReason = PathSegmentMissing;
                return false;
            }

            warningReason = null;
            return true;
        }

        if (current is IDictionary dictionary)
        {
            if (!dictionary.Contains(segment))
            {
                next = null;
                warningReason = PathSegmentMissing;
                return false;
            }

            next = dictionary[segment];
            warningReason = null;
            return true;
        }

        if (current is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            if (!jsonElement.TryGetProperty(segment, out var propertyValue))
            {
                next = null;
                warningReason = PathSegmentMissing;
                return false;
            }

            next = propertyValue;
            warningReason = null;
            return true;
        }

        next = null;
        warningReason = PathTraversalNotMapping;
        return false;
    }
}
