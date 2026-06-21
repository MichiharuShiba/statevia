namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>ModuleHost が load する 1 つの Action Module 単位。</summary>
/// <param name="ModuleDirectoryName">modules ルート直下のディレクトリ名。</param>
/// <param name="EntryAssemblyPath">entry DLL の絶対パス。</param>
/// <param name="SourceLabel">発見元ラベル（例: filesystem）。</param>
internal sealed record DiscoveredModule(
    string ModuleDirectoryName,
    string EntryAssemblyPath,
    string? SourceLabel = null);
