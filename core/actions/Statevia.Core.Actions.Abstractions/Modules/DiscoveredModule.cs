namespace Statevia.Core.Actions.Abstractions.Modules;

/// <summary>Source → ModuleHost 間で受け渡す、発見済み Action Module 1 件分の DTO。</summary>
/// <remarks>
/// <para>
/// <b>受け渡し DTO 兼 Catalog 登録モデル</b>であり、ローカルに実体化された正本である
/// <see cref="MaterializedModule"/> とは責務が異なる。Source が materialize した
/// <see cref="MaterializedModule"/> から本 DTO へ射影して ModuleHost へ渡す。
/// </para>
/// </remarks>
/// <param name="ModuleDirectoryName">modules ルート直下のディレクトリ名。</param>
/// <param name="EntryAssemblyPath">entry DLL の絶対パス。</param>
/// <param name="SourceLabel">発見元ラベル（例: filesystem / oci:{ref}）。</param>
public sealed record DiscoveredModule(
    string ModuleDirectoryName,
    string EntryAssemblyPath,
    string? SourceLabel = null);
