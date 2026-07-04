using System.Text.Json.Serialization;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>定義の作成リクエスト。</summary>
public sealed class CreateDefinitionRequest
{
    /// <summary>定義名。</summary>
    public string Name { get; set; } = "";

    /// <summary>定義ソース YAML。</summary>
    public string Yaml { get; set; } = "";
}

/// <summary>定義の更新リクエスト。</summary>
public sealed class UpdateDefinitionRequest
{
    /// <summary>定義名。</summary>
    public string Name { get; set; } = "";

    /// <summary>定義ソース YAML。</summary>
    public string Yaml { get; set; } = "";
}

/// <summary>定義の応答 DTO。</summary>
public sealed class DefinitionResponse
{
    /// <summary>表示用定義 ID。</summary>
    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = "";

    /// <summary>定義のリソース UUID。</summary>
    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    /// <summary>定義名。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>作成日時（UTC）。</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>更新日時（UTC）。</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>最新版番号（投影。truth は definition_versions）。</summary>
    [JsonPropertyName("latestVersion")]
    public int LatestVersion { get; set; }

    /// <summary>ソース YAML（取得時のみ。任意）。</summary>
    [JsonPropertyName("yaml")]
    public string? Yaml { get; set; }
}
