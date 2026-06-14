namespace Statevia.Actions.Abstractions.Publication;

/// <summary>識別・Marketplace・Documentation 向け表示メタデータ（design §13.2）。</summary>
/// <param name="ActionId">canonical actionId。</param>
/// <param name="Version">Action / Module のバージョン。</param>
/// <param name="DisplayName">一覧・フォーム向け表示名。</param>
/// <param name="Description">説明文（任意）。</param>
/// <param name="Category">カテゴリ（任意）。</param>
/// <param name="Icon">アイコン識別子（任意）。</param>
/// <param name="DocumentationUrl">ドキュメント URL（任意）。</param>
/// <param name="Tags">検索・分類タグ。</param>
/// <param name="Examples">入力例（任意）。</param>
public sealed record ActionDescriptor(
    string ActionId,
    string Version,
    string DisplayName,
    string? Description = null,
    string? Category = null,
    string? Icon = null,
    string? DocumentationUrl = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<ActionExample>? Examples = null);
