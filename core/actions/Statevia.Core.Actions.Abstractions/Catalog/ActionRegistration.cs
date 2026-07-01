namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Catalog から取得した Descriptor と実行エントリの組。</summary>
/// <param name="Descriptor">Action メタデータ。</param>
/// <param name="Entry">実行エントリ。</param>
public sealed record ActionRegistration(ActionDescriptor Descriptor, ActionCatalogEntry Entry);
