using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Service.Api.Contracts.Actions;

/// <summary>GET /v1/actions/schema のレスポンス本文。</summary>
public sealed class ActionSchemaListResponse
{
    /// <summary>登録 action の概要一覧。</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<ActionSchemaListItemDto> Items { get; init; } = [];
}

/// <summary>一覧項目（descriptor 概要）。</summary>
public sealed class ActionSchemaListItemDto
{
    /// <summary>canonical actionId。</summary>
    [JsonPropertyName("actionId")]
    public string ActionId { get; init; } = "";

    /// <summary>表示名。</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    /// <summary>バージョン。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    /// <summary>カテゴリ（任意）。</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Schema Publication が登録済みか。</summary>
    [JsonPropertyName("hasSchema")]
    public bool HasSchema { get; init; }
}

/// <summary>GET /v1/actions/schema/index のレスポンス本文。</summary>
public sealed class ActionSchemaIndexResponse
{
    /// <summary>Playground 向け軽量 index。</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<ActionSchemaIndexItemDto> Items { get; init; } = [];
}

/// <summary>Playground 向け軽量 index 項目。</summary>
public sealed class ActionSchemaIndexItemDto
{
    /// <summary>canonical actionId。</summary>
    [JsonPropertyName("actionId")]
    public string ActionId { get; init; } = "";

    /// <summary>表示名。</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    /// <summary>バージョン。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";
}

/// <summary>GET /v1/actions/schema/{actionId} のレスポンス本文。</summary>
public sealed class ActionSchemaDetailResponse
{
    /// <summary>識別・表示メタデータ。</summary>
    [JsonPropertyName("descriptor")]
    public ActionSchemaDescriptorDto Descriptor { get; init; } = new();

    /// <summary>input/output JSON Schema。</summary>
    [JsonPropertyName("schema")]
    public ActionSchemaBundleDto Schema { get; init; } = new();

    /// <summary>Playground UI ヒント（任意）。</summary>
    [JsonPropertyName("uiMetadata")]
    public ActionUiMetadataDto? UiMetadata { get; init; }
}

/// <summary>Schema API 向け ActionDescriptor DTO。</summary>
public sealed class ActionSchemaDescriptorDto
{
    /// <summary>canonical actionId。</summary>
    [JsonPropertyName("actionId")]
    public string ActionId { get; init; } = "";

    /// <summary>バージョン。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    /// <summary>表示名。</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    /// <summary>説明（任意）。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>カテゴリ（任意）。</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>アイコン識別子（任意）。</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>ドキュメント URL（任意）。</summary>
    [JsonPropertyName("documentationUrl")]
    [SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "JSON API contract; publication ActionDescriptor は string URL を返す。")]
    public string? DocumentationUrl { get; init; }

    /// <summary>検索タグ。</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>入力例（任意）。</summary>
    [JsonPropertyName("examples")]
    public IReadOnlyList<ActionExampleDto>? Examples { get; init; }
}

/// <summary>AI / Doc 向け action 入力例 DTO。</summary>
public sealed class ActionExampleDto
{
    /// <summary>例の表示名。</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    /// <summary>例示する input マップ。</summary>
    [JsonPropertyName("input")]
    public JsonElement Input { get; init; }
}

/// <summary>input/output JSON Schema DTO。</summary>
public sealed class ActionSchemaBundleDto
{
    /// <summary>input JSON Schema。</summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; init; }

    /// <summary>output JSON Schema。</summary>
    [JsonPropertyName("outputSchema")]
    public JsonElement OutputSchema { get; init; }

    /// <summary>Schema 方言（例: 2020-12）。</summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "";
}

/// <summary>Playground UI ヒント DTO。</summary>
public sealed class ActionUiMetadataDto
{
    /// <summary>フォーム表示順（任意）。</summary>
    [JsonPropertyName("fieldOrder")]
    public IReadOnlyList<string>? FieldOrder { get; init; }

    /// <summary>プロパティ名 → UI ヒント。</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, ActionFieldUiHintsDto>? Fields { get; init; }

    /// <summary>enum 値 → labelKey（任意）。</summary>
    [JsonPropertyName("enumLabelKeys")]
    public IReadOnlyDictionary<string, string>? EnumLabelKeys { get; init; }
}

/// <summary>単一 input プロパティの UI ヒント DTO。</summary>
public sealed class ActionFieldUiHintsDto
{
    /// <summary>widget 種別。</summary>
    [JsonPropertyName("widget")]
    public string? Widget { get; init; }

    /// <summary>i18n label キー。</summary>
    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; init; }

    /// <summary>i18n description キー。</summary>
    [JsonPropertyName("descriptionKey")]
    public string? DescriptionKey { get; init; }

    /// <summary>i18n placeholder キー。</summary>
    [JsonPropertyName("placeholderKey")]
    public string? PlaceholderKey { get; init; }

    /// <summary>機微値フィールドか。</summary>
    [JsonPropertyName("sensitive")]
    public bool Sensitive { get; init; }
}
