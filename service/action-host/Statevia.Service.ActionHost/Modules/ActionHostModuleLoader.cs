using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Engine.Abstractions;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.ActionHost.Modules;

/// <summary>filesystem 上の Action Module を ALC load し実行レジストリへ登録する。</summary>
internal sealed class ActionHostModuleLoader
{
    private readonly ActionHostActionRegistry _registry;
    private readonly FilesystemModuleDiscoverer _discoverer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<ActionHostOptions> _options;
    private readonly ILogger<ActionHostModuleLoader> _logger;
    private readonly object _sync = new();
    private readonly List<ModuleAssemblyLoadContext> _loadContexts = [];

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ActionHostModuleLoader(
        ActionHostActionRegistry registry,
        FilesystemModuleDiscoverer discoverer,
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        IOptions<ActionHostOptions> options,
        ILogger<ActionHostModuleLoader> logger)
    {
        _registry = registry;
        _discoverer = discoverer;
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _options = options;
        _logger = logger;
    }

    /// <summary>modules ルートを scan し未登録 Action を load する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    public void LoadAll(CancellationToken cancellationToken)
    {
        var modulesRoot = ResolveModulesRoot();
        var discovered = _discoverer.Discover(modulesRoot, cancellationToken);

        foreach (var module in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadModule(module);
        }
    }

    private string ResolveModulesRoot()
    {
        var configuredPath = _options.Value.ModulesPath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var trimmed = configuredPath.Trim();
            return Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, trimmed));
        }

        return ModulePathResolver.Resolve(
            _hostEnvironment.ContentRootPath,
            Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable),
            configurationPath: null);
    }

    private void LoadModule(DiscoveredModule discoveredModule)
    {
        lock (_sync)
        {
            LoadModuleCore(discoveredModule);
        }
    }

    private void LoadModuleCore(DiscoveredModule discoveredModule)
    {
        try
        {
            var loadContext = new ModuleAssemblyLoadContext(discoveredModule.EntryAssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(discoveredModule.EntryAssemblyPath);
            var moduleType = FindActionModuleType(assembly);
            if (moduleType is null)
            {
                ActionHostModuleLoaderLog.ModuleSkipped(
                    _logger,
                    discoveredModule.ModuleDirectoryName,
                    "IActionModule implementation not found");
                return;
            }

            if (Activator.CreateInstance(moduleType) is not IActionModule actionModule)
            {
                ActionHostModuleLoaderLog.ModuleSkipped(
                    _logger,
                    discoveredModule.ModuleDirectoryName,
                    $"Failed to instantiate IActionModule type '{moduleType.FullName}'");
                return;
            }

            _loadContexts.Add(loadContext);

            var registeredCount = 0;
            foreach (var registration in actionModule.GetActions(_serviceProvider))
            {
                if (string.IsNullOrWhiteSpace(registration.ActionId))
                {
                    continue;
                }

                if (registration.ExecutorFactory is null)
                {
                    ActionHostModuleLoaderLog.ActionSkipped(
                        _logger,
                        registration.ActionId,
                        actionModule.ModuleId,
                        "ExecutorFactory is required");
                    continue;
                }

                if (!registration.ActionId.Trim().StartsWith($"{actionModule.ModuleId}.", StringComparison.Ordinal))
                {
                    ActionHostModuleLoaderLog.ActionSkipped(
                        _logger,
                        registration.ActionId,
                        actionModule.ModuleId,
                        "namespace convention violation");
                    continue;
                }

                var executor = registration.ExecutorFactory(_serviceProvider);
                var loaded = new LoadedActionRegistration(
                    registration.ActionId.Trim(),
                    executor,
                    actionModule.ModuleId);

                if (_registry.TryRegister(loaded))
                {
                    registeredCount++;
                }
                else
                {
                    ActionHostModuleLoaderLog.DuplicateActionSkipped(
                        _logger,
                        registration.ActionId,
                        actionModule.ModuleId);
                }
            }

            ActionHostModuleLoaderLog.ModuleLoaded(
                _logger,
                actionModule.ModuleId,
                actionModule.Version,
                discoveredModule.EntryAssemblyPath,
                registeredCount);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or ArgumentException
            or IOException
            or UnauthorizedAccessException
            or BadImageFormatException
            or FileNotFoundException
            or FileLoadException
            or DirectoryNotFoundException
            or ReflectionTypeLoadException
            or TypeLoadException
            or MissingMethodException
            or TargetInvocationException)
        {
            ActionHostModuleLoaderLog.ModuleLoadFailed(_logger, ex, discoveredModule.EntryAssemblyPath);
        }
    }

    private static Type? FindActionModuleType(Assembly assembly) =>
        assembly.GetTypes()
            .FirstOrDefault(type =>
                typeof(IActionModule).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false });
}

internal static partial class ActionHostModuleLoaderLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Skipping module '{ModuleDirectory}': {Reason}")]
    public static partial void ModuleSkipped(ILogger logger, string moduleDirectory, string reason);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Skipping action '{ActionId}' from module {ModuleId}: {Reason}")]
    public static partial void ActionSkipped(ILogger logger, string actionId, string moduleId, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Skipping duplicate action '{ActionId}' from module {ModuleId}")]
    public static partial void DuplicateActionSkipped(ILogger logger, string actionId, string moduleId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Loaded module {ModuleId} v{Version} from {EntryAssemblyPath} ({RegisteredCount} action(s))")]
    public static partial void ModuleLoaded(
        ILogger logger,
        string moduleId,
        string version,
        string entryAssemblyPath,
        int registeredCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to load module from {EntryAssemblyPath}")]
    public static partial void ModuleLoadFailed(ILogger logger, Exception exception, string entryAssemblyPath);
}
