using Microsoft.Extensions.Logging;

namespace Statevia.Infrastructure.Modules;

/// <summary>ローカル filesystem から Action Module を発見する（load は行わない）。</summary>
internal sealed class FilesystemModuleSource : IModuleSource
{
    /// <summary>
    /// Filesystem Source の既定優先度。リモート Source（OCI 等）より高くも低くもない
    /// 中位値を採り、単独利用時の後方互換（従来挙動）を保つための基準点とする。
    /// </summary>
    internal const int DefaultPriority = 100;

    private readonly IResolvedModulePathProvider _pathProvider;
    private readonly ILogger<FilesystemModuleSource> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="pathProvider">解決済み modules ルート。</param>
    /// <param name="logger">ログ。</param>
    public FilesystemModuleSource(
        IResolvedModulePathProvider pathProvider,
        ILogger<FilesystemModuleSource> logger)
    {
        _pathProvider = pathProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Priority => DefaultPriority;

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var modulesRoot = _pathProvider.ModulesRoot;
        if (!Directory.Exists(modulesRoot))
        {
            FilesystemModuleSourceLog.ModulesRootMissing(_logger, modulesRoot);
            return Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
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
                FilesystemModuleSourceLog.ModuleDirectorySkipped(_logger, moduleDirectoryName, reason);
                continue;
            }


            discovered.Add(new DiscoveredModule(
                moduleDirectoryName,
                entryAssemblyPath,
                SourceLabel: "filesystem"));
        }

        return Task.FromResult<IReadOnlyList<DiscoveredModule>>(discovered);
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

internal static partial class FilesystemModuleSourceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Modules root does not exist yet: {ModulesRoot}")]
    public static partial void ModulesRootMissing(ILogger logger, string modulesRoot);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Skipping module directory '{ModuleDirectory}': {Reason}")]
    public static partial void ModuleDirectorySkipped(ILogger logger, string moduleDirectory, string reason);
}
