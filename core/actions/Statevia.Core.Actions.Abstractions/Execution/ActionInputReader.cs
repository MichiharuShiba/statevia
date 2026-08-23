using System.Globalization;
using System.Text.Json;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Engine から渡される Action 入力を JSON オブジェクトとして読み取る。</summary>
/// <remarks>
/// <para>
/// Builtin と Action Module の双方が同じ契約で入力を解釈する。ホストは
/// <c>Statevia.Core.Actions.Abstractions</c> を共有するため、Module 側に同等実装をコピーしない。
/// </para>
/// <para>フィールド名は大文字小文字を無視する。JSON 以外の CLR オブジェクトは camelCase で直列化する。</para>
/// </remarks>
public static class ActionInputReader
{
    private static readonly JsonSerializerOptions s_serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyFields =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    /// <summary>入力を JSON オブジェクトとして読み取る。</summary>
    /// <param name="input">Engine が解決した入力。</param>
    /// <param name="fields">オブジェクトのフィールド。失敗時は空。</param>
    /// <returns>オブジェクトとして解釈できたとき <see langword="true"/>。</returns>
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
    /// <param name="fields"><see cref="TryReadObject"/> が返したフィールド。</param>
    /// <param name="name">フィールド名。</param>
    /// <returns>空白でない文字列。</returns>
    /// <exception cref="ArgumentException">欠落、非文字列、または空白のとき。</exception>
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
    /// <param name="fields"><see cref="TryReadObject"/> が返したフィールド。</param>
    /// <param name="name">フィールド名。</param>
    /// <returns>文字列値。欠落または非文字列のとき <see langword="null"/>。</returns>
    public static string? OptionalString(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>duration 文字列または数値（ミリ秒）を <see cref="TimeSpan"/> に変換する。</summary>
    /// <param name="fields"><see cref="TryReadObject"/> が返したフィールド。</param>
    /// <param name="name">フィールド名。既定は <c>duration</c>。</param>
    /// <returns>解釈した期間。</returns>
    /// <exception cref="ArgumentException">欠落、または文字列・数値以外のとき。</exception>
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
    /// <param name="raw"><c>5s</c> / <c>500ms</c> / 単位なし秒数。</param>
    /// <returns>解釈した期間。</returns>
    /// <exception cref="ArgumentException">空、または解釈できない形式のとき。</exception>
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

    /// <summary>
    /// Engine は既に <see cref="JsonElement"/> を渡すことがある。それ以外は camelCase JSON に直列化する。
    /// </summary>
    private static JsonElement ToJsonElement(object input) =>
        input switch
        {
            JsonElement jsonElement => jsonElement,
            _ => JsonSerializer.SerializeToElement(input, s_serializeOptions),
        };
}
