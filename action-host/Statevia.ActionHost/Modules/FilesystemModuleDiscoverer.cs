using Microsoft.Extensions.Logging;

namespace Statevia.ActionHost.Modules;

/// <summary>modules ルートから entry DLL を列挙する（load は行わない）。</summary>
internal sealed class FilesystemModuleDiscoverer
{
    private readonly ILogger<FilesystemModuleDiscoverer> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="logger">ログ。</param>
    public FilesystemModuleDiscoverer(ILogger<FilesystemModuleDiscoverer> logger) =>
        _logger = logger;

    /// <summary>modules ルート配下の Action Module を発見する。</summary>
    /// <param name="modulesRoot">modules ルート絶対パス。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>発見済み module 一覧。</returns>
    public IReadOnlyList<DiscoveredModule> Discover(string modulesRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(modulesRoot))
        {
            FilesystemModuleDiscovererLog.ModulesRootMissing(_logger, modulesRoot);
            return Array.Empty<DiscoveredModule>();
        }

        var discovered = new List<DiscoveredModule>();
        foreach (var moduleDirectory in Directory.EnumerateDirectories(modulesRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var moduleDirectoryName = Path.GetFileName(moduleDirectory);
            if (string.IsNullOrWhiteSpace(moduleDirectoryName))
            {
                continue;
            }

            if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason))
            {
                FilesystemModuleDiscovererLog.ModuleDirectorySkipped(_logger, moduleDirectoryName, reason);
                continue;
            }

            discovered.Add(new DiscoveredModule(moduleDirectoryName, entryAssemblyPath));
        }

        return discovered;
    }

    internal static bool TryResolveEntryAssemblyPath(
        string moduleDirectory,
        string moduleDirectoryName,
        out string entryAssemblyPath,
        out string reason)
    {
        entryAssemblyPath = string.Empty;
        reason = string.Empty;

        var preferred = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        if (File.Exists(preferred))
        {
            entryAssemblyPath = Path.GetFullPath(preferred);
            return true;
        }

        var dllFiles = Directory.EnumerateFiles(moduleDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return dllFiles.Count switch
        {
            0 => Fail("no entry DLL found", out entryAssemblyPath, out reason),
            1 => Success(dllFiles[0], out entryAssemblyPath, out reason),
            _ => Fail("multiple DLL files found; expected a single entry DLL or {name}.dll", out entryAssemblyPath, out reason),
        };

        static bool Success(string path, out string resolved, out string skipReason)
        {
            resolved = path;
            skipReason = string.Empty;
            return true;
        }

        static bool Fail(string message, out string resolved, out string skipReason)
        {
            resolved = string.Empty;
            skipReason = message;
            return false;
        }
    }
}

internal static partial class FilesystemModuleDiscovererLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Modules root does not exist yet: {ModulesRoot}")]
    public static partial void ModulesRootMissing(ILogger logger, string modulesRoot);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Skipping module directory '{ModuleDirectory}': {Reason}")]
    public static partial void ModuleDirectorySkipped(ILogger logger, string moduleDirectory, string reason);
}
