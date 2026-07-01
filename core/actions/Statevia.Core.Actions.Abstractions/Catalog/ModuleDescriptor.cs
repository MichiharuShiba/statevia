namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Module 単位の Trust / Source / 署名メタデータ。</summary>
/// <param name="ModuleId">Module の一意識別子。</param>
/// <param name="Version">Module バージョン。</param>
/// <param name="Publisher">公開者情報。</param>
/// <param name="TrustLevel">信頼レベル。</param>
/// <param name="Source">導入元。</param>
/// <param name="Signature">署名（任意。Phase 3 で検証）。</param>
/// <param name="ActionIds">登録された canonical actionId 一覧。</param>
public sealed record ModuleDescriptor(
    string ModuleId,
    string Version,
    ActionPublisher Publisher,
    ActionTrustLevel TrustLevel,
    ActionSourceKind Source,
    ActionSignature? Signature,
    IReadOnlyList<string> ActionIds);
