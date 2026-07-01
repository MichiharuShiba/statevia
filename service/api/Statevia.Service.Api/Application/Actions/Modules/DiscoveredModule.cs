namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>Source → ModuleHost 間で受け渡す、発見済み Action Module 1 件分の DTO。</summary>
/// <remarks>
/// <para>
/// Core-API 内部の <b>受け渡し DTO 兼 Catalog 登録モデル</b>であり、ローカルに実体化された正本である
/// <see cref="Statevia.Modules.MaterializedModule"/> とは責務が異なる。Source が materialize した
/// <c>MaterializedModule</c> から本 DTO へ射影して ModuleHost へ渡す（射影は供給パイプライン側＝B1 で実装）。
/// </para>
/// <para>将来、<c>MaterializedModule</c> との統合・名称変更の余地を残す。</para>
/// </remarks>
/// <param name="ModuleDirectoryName">modules ルート直下のディレクトリ名。</param>
/// <param name="EntryAssemblyPath">entry DLL の絶対パス。</param>
/// <param name="SourceLabel">発見元ラベル（例: filesystem / oci:{ref}）。</param>
internal sealed record DiscoveredModule(
    string ModuleDirectoryName,
    string EntryAssemblyPath,
    string? SourceLabel = null);

