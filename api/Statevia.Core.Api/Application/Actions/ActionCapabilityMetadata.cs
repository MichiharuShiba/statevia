namespace Statevia.Core.Api.Application.Actions;

/// <summary>登録済みアクションの Capability メタデータ（UI 発見性・experimental 表示用）。</summary>
/// <param name="Category">Capability 分類。</param>
/// <param name="DisplayName">表示名。</param>
/// <param name="IsExperimental">experimental 機能かどうか。</param>
internal sealed record ActionCapabilityMetadata(
    ActionCapabilityCategory Category,
    string DisplayName,
    bool IsExperimental = false);
