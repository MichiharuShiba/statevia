using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Statevia.Core.Application.Infrastructure;

/// <summary>
/// <see cref="JsonSerializer"/> の復元に限定した薄いヘルパー。
/// DB キャッシュ／投影 JSON など、不健全な入力でも呼び出し側を落とさない経路でのみ利用する。
/// </summary>
internal static class JsonDeserialize
{
    /// <summary>
    /// 実行グラフスナップショット等、歴史的に PascalCase も混在する JSON をゆるやかに解釈するとき。
    /// </summary>
    internal static readonly JsonSerializerOptions CaseInsensitiveDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions?)"/> を試し、復元処理で想定する例外のみ失敗として扱う。
    /// </summary>
    /// <returns><c>true</c> のとき復帰値を解釈可能（入力が JSON の <c>null</c> であれば <paramref name="value"/> は既定の参照型どおりになりうる）。</returns>
    internal static bool TryDeserialize<T>(
        string json,
        JsonSerializerOptions? options,
        [MaybeNullWhen(false)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            value = JsonSerializer.Deserialize<T>(json, options);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
        catch (InvalidOperationException)
        {
            value = default;
            return false;
        }
        catch (NotSupportedException)
        {
            value = default;
            return false;
        }
    }
}
