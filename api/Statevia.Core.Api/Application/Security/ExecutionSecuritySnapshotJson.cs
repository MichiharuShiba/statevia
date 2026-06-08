using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Core.Api.Application.Security;

/// <summary><see cref="ExecutionSecuritySnapshot"/> の JSON 永続化。</summary>
public static class ExecutionSecuritySnapshotJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>スナップショットを JSON 文字列へシリアル化する。</summary>
    /// <param name="snapshot">スナップショット。</param>
    public static string Serialize(ExecutionSecuritySnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SerializerOptions);

    /// <summary>JSON 文字列からスナップショットを復元する。</summary>
    /// <param name="json">永続化 JSON。空の場合は <see langword="null"/>。</param>
    public static ExecutionSecuritySnapshot? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ExecutionSecuritySnapshot>(json, SerializerOptions);
    }
}
