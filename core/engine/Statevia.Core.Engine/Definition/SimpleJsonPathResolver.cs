using System.Collections;
using System.Text.Json;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// SimpleJsonPath セグメント列に沿ってオブジェクトを辿る解決器。
/// </summary>
internal static class SimpleJsonPathResolver
{
    /// <summary>path が <c>$.</c> / <c>$[</c> で始まらず、フォールバック判断が必要。</summary>
    public const string IgnoredNonDollarDotPath = "IgnoredNonDollarDotPath";

    /// <summary>JSONPath 風セグメントが辞書に存在しない。</summary>
    public const string PathSegmentMissing = "PathSegmentMissing";

    /// <summary>中間値がマッピング型ではなくトラバース不能。</summary>
    public const string PathTraversalNotMapping = "PathTraversalNotMapping";

    /// <summary>配列インデックスが範囲外。</summary>
    public const string PathIndexOutOfRange = "PathIndexOutOfRange";

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
    /// <param name="source">解決の根オブジェクト。</param>
    /// <param name="path">SimpleJsonPath 文字列。</param>
    /// <returns>解決結果。</returns>
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

        if (!SimpleJsonPath.TryGetSegments(path, out var segments))
        {
            var ignored = SimpleJsonPath.IsPathExpression(path)
                ? null
                : IgnoredNonDollarDotPath;
            return new ResolveResult(
                IsSupportedPathExpression: false,
                Found: false,
                Value: source,
                WarningReason: ignored);
        }

        object? current = source;
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

    private static bool TryGetSegmentValue(
        object? current,
        PathSegment segment,
        out object? next,
        out string? warningReason) =>
        segment.Kind switch
        {
            PathSegmentKind.Identifier or PathSegmentKind.QuotedKey =>
                TryGetNamedSegmentValue(current, segment.Name!, out next, out warningReason),
            PathSegmentKind.ArrayIndex =>
                TryGetIndexedSegmentValue(current, segment.Index!.Value, out next, out warningReason),
            _ => FailUnknownSegment(out next, out warningReason),
        };

    private static bool FailUnknownSegment(out object? next, out string? warningReason)
    {
        next = null;
        warningReason = PathTraversalNotMapping;
        return false;
    }

    private static bool TryGetNamedSegmentValue(
        object? current,
        string segment,
        out object? next,
        out string? warningReason)
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

    private static bool TryGetIndexedSegmentValue(
        object? current,
        int index,
        out object? next,
        out string? warningReason)
    {
        if (current is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            if (index >= jsonElement.GetArrayLength())
            {
                next = null;
                warningReason = PathIndexOutOfRange;
                return false;
            }

            next = jsonElement[index];
            warningReason = null;
            return true;
        }

        if (current is Array array)
        {
            if (index >= array.Length)
            {
                next = null;
                warningReason = PathIndexOutOfRange;
                return false;
            }

            next = array.GetValue(index);
            warningReason = null;
            return true;
        }

        if (current is IList list)
        {
            if (index >= list.Count)
            {
                next = null;
                warningReason = PathIndexOutOfRange;
                return false;
            }

            next = list[index];
            warningReason = null;
            return true;
        }

        if (current is IReadOnlyList<object?> readOnlyList)
        {
            if (index >= readOnlyList.Count)
            {
                next = null;
                warningReason = PathIndexOutOfRange;
                return false;
            }

            next = readOnlyList[index];
            warningReason = null;
            return true;
        }

        next = null;
        warningReason = PathTraversalNotMapping;
        return false;
    }
}
