namespace Statevia.Core.Actions.Abstractions.Publication;

/// <summary>Playground 向け UI ヒント（検証ロジックを含まない）。</summary>
/// <param name="FieldOrder">フォーム表示順（任意）。</param>
/// <param name="Fields">プロパティ名 → UI ヒント。</param>
/// <param name="EnumLabelKeys">enum 値 → labelKey（任意）。</param>
public sealed record ActionUiMetadata(
    IReadOnlyList<string>? FieldOrder = null,
    IReadOnlyDictionary<string, ActionFieldUiHints>? Fields = null,
    IReadOnlyDictionary<string, string>? EnumLabelKeys = null);

/// <summary>単一 input プロパティの UI ヒント。</summary>
/// <param name="Widget">widget 種別（text / select / secret / url 等）。</param>
/// <param name="LabelKey">i18n label キー（canonical actionId 根）。</param>
/// <param name="DescriptionKey">i18n description キー（任意）。</param>
/// <param name="PlaceholderKey">i18n placeholder キー（任意）。</param>
/// <param name="Sensitive">機微値フィールドか（マスク表示）。</param>
public sealed record ActionFieldUiHints(
    string? Widget = null,
    string? LabelKey = null,
    string? DescriptionKey = null,
    string? PlaceholderKey = null,
    bool Sensitive = false);
