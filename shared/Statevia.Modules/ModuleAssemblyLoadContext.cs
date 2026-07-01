using System.Reflection;
using System.Runtime.Loader;

namespace Statevia.Modules;

/// <summary>1 Action Module = 1 隔離 ALC。プラットフォーム契約は Default コンテキストを共有する。</summary>
/// <remarks>
/// <para>
/// Core-API（<c>ModuleHost</c>）と Action Host（<c>ActionHostModuleLoader</c>）の両方から利用する
/// ALC 隔離契約の単一正本。Module の entry DLL とその依存は隔離 ALC に load する一方、
/// プラットフォーム契約アセンブリ（<see cref="SharedAssemblyNames"/>）は Default コンテキストの
/// 同一型を共有し、ホスト側と Module 側で型が一致するようにする。
/// </para>
/// <para>
/// 共通化の範囲は ALC・プラットフォーム契約の共有 load のみとする。
/// <c>IActionModule</c> 実装型の探索や entry DLL パス解決は、ホストごとに登録先
/// （Catalog / 実行レジストリ）やテナント文脈が異なるため各ローダー側に残す。
/// </para>
/// </remarks>
public sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// Default コンテキストと共有するプラットフォーム契約アセンブリの単純名。
    /// ホスト側と Module 側で同一型を指すよう、隔離 ALC では再 load せず Default から解決する。
    /// </summary>
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Statevia.Modules",
        "Statevia.Core.Actions.Abstractions",
        "Statevia.Core.Engine",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
    };

    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>entry assembly パスから ALC を構築する。</summary>
    /// <param name="assemblyPath">entry DLL の絶対パス。</param>
    public ModuleAssemblyLoadContext(string assemblyPath)
        : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblySimpleName = assemblyName.Name;
        if (assemblySimpleName is not null && SharedAssemblyNames.Contains(assemblySimpleName))
        {
            var shared = Default.Assemblies.FirstOrDefault(
                assembly => string.Equals(assembly.GetName().Name, assemblySimpleName, StringComparison.OrdinalIgnoreCase));
            if (shared is not null)
            {
                return shared;
            }
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
