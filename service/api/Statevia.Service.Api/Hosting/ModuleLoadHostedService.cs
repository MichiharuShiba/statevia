using System.Reflection;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Modules;
using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Hosting;

/// <summary>起動時 Module scan と add-only filesystem watcher。</summary>
internal sealed class ModuleLoadHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly ModuleLoadHostedServiceDependencies _dependencies;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ModuleHostOptions> _options;
    private readonly ILogger<ModuleLoadHostedService> _logger;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _debounceSync = new();
    private string? _ownerTenantId;
    private bool _disposed;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ModuleLoadHostedService(
        ModuleLoadHostedServiceDependencies dependencies,
        IServiceScopeFactory scopeFactory,
        IOptions<ModuleHostOptions> options,
        ILogger<ModuleLoadHostedService> logger)
    {
        _dependencies = dependencies;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ownerTenantId = await ResolveOwnerTenantIdAsync(cancellationToken).ConfigureAwait(false);
        await _dependencies.ModuleHost.LoadAsync(_ownerTenantId, cancellationToken).ConfigureAwait(false);
        StartWatcher();
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopWatcher();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatcher();
        _debounceTimer?.Dispose();
    }

    private void StartWatcher()
    {
        var modulesRoot = _dependencies.PathProvider.ModulesRoot;
        Directory.CreateDirectory(modulesRoot);

        _watcher = new FileSystemWatcher(modulesRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        _watcher.Created += OnFilesystemCreated;
        _watcher.Changed += OnFilesystemChangedOrDeleted;
        _watcher.Deleted += OnFilesystemChangedOrDeleted;
        _watcher.Renamed += OnFilesystemRenamed;
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Created -= OnFilesystemCreated;
        _watcher.Changed -= OnFilesystemChangedOrDeleted;
        _watcher.Deleted -= OnFilesystemChangedOrDeleted;
        _watcher.Renamed -= OnFilesystemRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFilesystemCreated(object sender, FileSystemEventArgs e) =>
        ScheduleReload();

    private void OnFilesystemChangedOrDeleted(object sender, FileSystemEventArgs e) =>
        ModuleLoadHostedServiceLog.ModuleChangeRequiresRestart(_logger, e.FullPath);

    private void OnFilesystemRenamed(object sender, RenamedEventArgs e) =>
        ModuleLoadHostedServiceLog.ModuleRenameRequiresRestart(_logger, e.OldFullPath, e.FullPath);

    private void ScheduleReload()
    {
        lock (_debounceSync)
        {
            _debounceTimer ??= new Timer(
                static state => ((ModuleLoadHostedService)state!).TriggerDebouncedReload(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void TriggerDebouncedReload() =>
        _ = DebouncedReloadAsync();

    private async Task DebouncedReloadAsync()
    {
        if (_ownerTenantId is null)
        {
            return;
        }

        try
        {
            await _dependencies.ModuleHost.LoadAsync(_ownerTenantId, CancellationToken.None).ConfigureAwait(false);
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
            ModuleLoadHostedServiceLog.ModuleReloadFailed(_logger, ex);
        }
    }


    private async Task<string> ResolveOwnerTenantIdAsync(CancellationToken cancellationToken)
    {
        var configured = _options.Value.OwnerTenantId;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDataAccess = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        var tenant = await platformDataAccess
            .FindTenantByKeyAsync(TenantHeader.DefaultTenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            throw new InvalidOperationException(
                "Default tenant not found. Ensure tenant bootstrap completed before module load.");
        }

        return tenant.TenantId.ToString("D");
    }
}

/// <summary><see cref="ModuleLoadHostedService"/> の依存を束ねる。</summary>
internal sealed class ModuleLoadHostedServiceDependencies
{
    /// <summary>新しいインスタンスを初期化する。</summary>
    public ModuleLoadHostedServiceDependencies(ModuleHost moduleHost, IResolvedModulePathProvider pathProvider)
    {
        ModuleHost = moduleHost;
        PathProvider = pathProvider;
    }

    /// <summary>Module load ホスト。</summary>
    public ModuleHost ModuleHost { get; }

    /// <summary>modules ルート。</summary>
    public IResolvedModulePathProvider PathProvider { get; }
}

internal static partial class ModuleLoadHostedServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Module change detected at {Path}. Restart required for updates or removals.")]
    public static partial void ModuleChangeRequiresRestart(ILogger logger, string path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Module rename detected from {OldPath} to {NewPath}. Restart required.")]
    public static partial void ModuleRenameRequiresRestart(ILogger logger, string oldPath, string newPath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to load newly discovered modules")]
    public static partial void ModuleReloadFailed(ILogger logger, Exception exception);
}
