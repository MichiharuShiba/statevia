namespace Statevia.Core.Actions.Abstractions.Publication;

/// <summary>Module 登録時の公開単位（Executor と一体で Catalog へ登録）。</summary>
/// <param name="Descriptor">表示・Marketplace メタデータ。</param>
/// <param name="SchemaBundle">input/output JSON Schema。</param>
/// <param name="UiMetadata">Playground UI ヒント（任意）。</param>
public sealed record ActionPublication(
    ActionDescriptor Descriptor,
    ActionSchemaBundle SchemaBundle,
    ActionUiMetadata? UiMetadata = null);
