using System.Text.Json;

namespace Statevia.Core.Engine.Engine;

/// <summary>チェックポイント向け JSON 変換ヘルパー。</summary>
internal static class CheckpointJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>任意オブジェクトを JsonElement に変換する（null は null）。</summary>
    public static JsonElement? ToElement(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.Clone();
        }

        return JsonSerializer.SerializeToElement(value, s_options);
    }

    /// <summary>JsonElement を object? に戻す（オブジェクト/配列は Dictionary/List）。</summary>
    public static object? FromElement(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return FromElement(element.Value);
    }

    /// <summary>JsonElement を object? に戻す。</summary>
    public static object? FromElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l
                : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(FromElement)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => FromElement(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
}
