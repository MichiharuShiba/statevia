using System.Globalization;
using System.Text.Json;

namespace Statevia.Core.Api.Application.Actions.Builtins;

/// <summary>Engine から渡される action 入力を Builtin 向けに読み取る。</summary>
internal static class ActionInputReader
{
    private static readonly JsonSerializerOptions s_serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>入力を JSON オブジェクトとして読み取る。</summary>
    /// <param name="input">Engine が解決した入力。</param>
    /// <param name="fields">オブジェクトのフィールド。</param>
    public static bool TryReadObject(object? input, out IReadOnlyDictionary<string, JsonElement> fields)
    {
        fields = EmptyFields;
        if (input is null)
        {
            return false;
        }

        var root = ToJsonElement(input);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        fields = root.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>必須文字列フィールドを取得する。</summary>
    public static string RequireString(IReadOnlyDictionary<string, JsonElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Action input field '{name}' is required.");
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"Action input field '{name}' must not be empty.");
        }

        return text;
    }

    /// <summary>任意文字列フィールドを取得する。</summary>
    public static string? OptionalString(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>duration 文字列または数値（ミリ秒）を <see cref="TimeSpan"/> に変換する。</summary>
    public static TimeSpan ParseDuration(IReadOnlyDictionary<string, JsonElement> fields, string name = "duration")
    {
        if (!fields.TryGetValue(name, out var value))
        {
            throw new ArgumentException($"Action input field '{name}' is required.");
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => ParseDurationString(value.GetString()!),
            JsonValueKind.Number when value.TryGetInt64(out var milliseconds) =>
                TimeSpan.FromMilliseconds(milliseconds),
            _ => throw new ArgumentException($"Action input field '{name}' must be a duration string or number."),
        };
    }

    /// <summary>duration 文字列を <see cref="TimeSpan"/> に変換する。</summary>
    public static TimeSpan ParseDurationString(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = raw.Trim();

        if (trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(trimmed[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        if (trimmed.EndsWith('s')
            && double.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericSeconds))
        {
            return TimeSpan.FromSeconds(numericSeconds);
        }

        throw new ArgumentException($"Invalid duration value '{raw}'.");
    }

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyFields =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    private static JsonElement ToJsonElement(object input) =>
        input switch
        {
            JsonElement jsonElement => jsonElement,
            _ => JsonSerializer.SerializeToElement(input, s_serializeOptions),
        };
}
