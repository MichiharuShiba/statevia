using Statevia.Modules;

namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>
/// リモート取得を伴う <see cref="IModuleSource"/> の共通基底。取得元に依らず ModuleHost が
/// 同一手順で load できるよう、Module を <see cref="MaterializedModule"/>（ローカル正本）へ
/// materialize し、受け渡し DTO <see cref="DiscoveredModule"/> へ射影する責務を集約する。
/// </summary>
/// <remarks>
/// <para>
/// 派生 Source（例: OCI）は <see cref="MaterializeModulesAsync"/> に
/// 「acquire → cache(digest) → verify → extract → materialize」の取得パイプラインを実装する。
/// 取得経路ごとに異なる具体手順は派生側へ、共通の射影と entry DLL 解決は本基底へ寄せる。
/// </para>
/// <para>
/// entry DLL 解決は <see cref="FilesystemModuleSource.TryResolveEntryAssemblyPath"/> を共有し、
/// 取得経路に依らず同一の規約（<c>{name}.dll</c> 優先・単一 DLL 許容）を適用する。
/// </para>
/// <para>
/// 本基底は materialize 済みの正本を前提とし、署名検証（<c>ModuleSignatureVerifier</c>）は
/// ModuleHost の load 時に <see cref="DiscoveredModule.EntryAssemblyPath"/> へ適用される。
/// </para>
/// </remarks>
internal abstract class MaterializingModuleSourceBase : IModuleSource
{
    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <summary>
    /// 取得元種別（例: <c>oci</c>）。<see cref="MaterializedModule.SourceLabel"/> 未指定時の
    /// 既定ラベルとして用い、可観測性のため discover 結果へ伝播する。
    /// </summary>
    protected abstract string SourceType { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var materialized = await MaterializeModulesAsync(cancellationToken).ConfigureAwait(false);
        return materialized.Select(ToDiscoveredModule).ToList();
    }

    /// <summary>
    /// 取得元から Module を取得・検証・展開し、ローカル正本 <see cref="MaterializedModule"/> を返す。
    /// </summary>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>materialize 済み Module の一覧（0 件可）。</returns>
    protected abstract Task<IReadOnlyList<MaterializedModule>> MaterializeModulesAsync(CancellationToken cancellationToken);

    /// <summary>正本 <see cref="MaterializedModule"/> を受け渡し DTO <see cref="DiscoveredModule"/> へ射影する。</summary>
    /// <param name="materialized">materialize 済み正本。</param>
    /// <returns>ModuleHost へ渡す DTO。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="materialized"/> が <see langword="null"/> の場合。</exception>
    protected DiscoveredModule ToDiscoveredModule(MaterializedModule materialized)
    {
        ArgumentNullException.ThrowIfNull(materialized);

        var directoryName = Path.GetFileName(
            materialized.ModuleDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return new DiscoveredModule(
            directoryName,
            materialized.EntryAssemblyPath,
            materialized.SourceLabel ?? SourceType);
    }

    /// <summary>entry DLL 解決を <see cref="FilesystemModuleSource"/> と共有する。</summary>
    /// <param name="moduleDirectory">Module ディレクトリ。</param>
    /// <param name="moduleDirectoryName">Module ディレクトリ名。</param>
    /// <param name="entryAssemblyPath">解決された entry DLL の絶対パス。</param>
    /// <param name="reason">解決失敗理由（成功時は空）。</param>
    /// <returns>解決に成功した場合 <see langword="true"/>。</returns>
    protected static bool TryResolveEntryAssemblyPath(
        string moduleDirectory,
        string moduleDirectoryName,
        out string entryAssemblyPath,
        out string reason) =>
        FilesystemModuleSource.TryResolveEntryAssemblyPath(
            moduleDirectory,
            moduleDirectoryName,
            out entryAssemblyPath,
            out reason);
}
