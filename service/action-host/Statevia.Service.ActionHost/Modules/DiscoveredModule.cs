namespace Statevia.Service.ActionHost.Modules;

/// <summary>filesystem から発見した 1 Action Module。</summary>
/// <param name="ModuleDirectoryName">modules ルート直下のディレクトリ名。</param>
/// <param name="EntryAssemblyPath">entry DLL の絶対パス。</param>
internal sealed record DiscoveredModule(string ModuleDirectoryName, string EntryAssemblyPath);
