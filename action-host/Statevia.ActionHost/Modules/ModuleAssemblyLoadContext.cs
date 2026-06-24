using System.Reflection;
using System.Runtime.Loader;

namespace Statevia.ActionHost.Modules;

/// <summary>1 Action Module = 1 隔離 ALC。プラットフォーム契約は Default コンテキストを共有する。</summary>
internal sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Statevia.Modules",
        "Statevia.Actions.Abstractions",
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
