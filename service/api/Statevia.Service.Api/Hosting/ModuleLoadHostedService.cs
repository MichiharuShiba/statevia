using System.Reflection;
using Microsoft.Extensions.Options;
using Statevia.Infrastructure.Modules;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Hosting;

/// <summary>起動時 Module scan と add-only filesystem watcher。</summary>
/// <remarks>
/// 起動時は Active テナントごとに filesystem discover し、続けて remote Source を
/// default（または設定の OwnerTenantId）所属として load する。
/// watcher は作成パス先頭セグメントから tenant_key を推定し、当該テナントのみ reload する。
/// </remarks>
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
    private string? _pendingReloadPath;
    private string? _configuredOwnerTenantId;
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
        _configuredOwnerTenantId = NormalizeOptional(_options.Value.OwnerTenantId);
        await LoadAllTenantsAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task LoadAllTenantsAsync(CancellationToken cancellationToken)
    {
        // OwnerTenantId 固定時は DB 列挙不要（テスト / レガシー）。filesystem は default 配下のみ。
        if (_configuredOwnerTenantId is not null)
        {
            await LoadTenantModulesAsync(
                    _configuredOwnerTenantId,
                    TenantRequestHeaders.DefaultTenantId,
                    discoverFilesystem: true,
                    discoverRemote: false,
                    cancellationToken)
                .ConfigureAwait(false);
            await LoadTenantModulesAsync(
                    _configuredOwnerTenantId,
                    TenantRequestHeaders.DefaultTenantId,
                    discoverFilesystem: false,
                    discoverRemote: true,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDataAccess = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        var tenants = await platformDataAccess.ListActiveTenantsAsync(cancellationToken).ConfigureAwait(false);
        if (tenants.Count == 0)
        {
            throw new InvalidOperationException(
                "No active tenants found. Ensure tenant bootstrap completed before module load.");
        }

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadTenantModulesAsync(
                    tenant.TenantId.ToString("D"),
                    tenant.TenantKey,
                    discoverFilesystem: true,
                    discoverRemote: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var defaultTenant = ResolveDefaultTenant(tenants);
        await LoadTenantModulesAsync(
                defaultTenant.TenantId.ToString("D"),
                defaultTenant.TenantKey,
                discoverFilesystem: false,
                discoverRemote: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task LoadTenantModulesAsync(
        string ownerTenantId,
        string filesystemTenantKey,
        bool discoverFilesystem,
        bool discoverRemote,
        CancellationToken cancellationToken) =>
        _dependencies.ModuleHost.LoadAsync(
            ownerTenantId,
            cancellationToken,
            filesystemTenantKey,
            discoverFilesystem,
            discoverRemote);

    private static TenantRow ResolveDefaultTenant(IReadOnlyList<TenantRow> tenants) =>
        tenants.FirstOrDefault(t =>
            string.Equals(t.TenantKey, TenantRequestHeaders.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        ?? tenants[0];

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
        ScheduleReload(e.FullPath);

    private void OnFilesystemChangedOrDeleted(object sender, FileSystemEventArgs e) =>
        ModuleLoadHostedServiceLog.ModuleChangeRequiresRestart(_logger, e.FullPath);

    private void OnFilesystemRenamed(object sender, RenamedEventArgs e) =>
        ModuleLoadHostedServiceLog.ModuleRenameRequiresRestart(_logger, e.OldFullPath, e.FullPath);

    private void ScheduleReload(string fullPath)
    {
        lock (_debounceSync)
        {
            _pendingReloadPath = fullPath;
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
        string? path;
        lock (_debounceSync)
        {
            path = _pendingReloadPath;
            _pendingReloadPath = null;
        }

        if (path is null)
        {
            return;
        }

        try
        {
            await ReloadForPathAsync(path, CancellationToken.None).ConfigureAwait(false);
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

    private async Task ReloadForPathAsync(string fullPath, CancellationToken cancellationToken)
    {
        var modulesRoot = _dependencies.PathProvider.ModulesRoot;
        var tenantKey = TryResolveTenantKeyFromPath(modulesRoot, fullPath);
        if (tenantKey is null)
        {
            ModuleLoadHostedServiceLog.UnscopedModulePathIgnored(_logger, fullPath);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDataAccess = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        var tenant = await platformDataAccess
            .FindTenantByKeyAsync(tenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            ModuleLoadHostedServiceLog.UnknownTenantDirectory(_logger, tenantKey, fullPath);
            return;
        }

        var ownerTenantId = _configuredOwnerTenantId ?? tenant.TenantId.ToString("D");
        await _dependencies.ModuleHost
            .LoadAsync(
                ownerTenantId,
                cancellationToken,
                filesystemTenantKey: tenant.TenantKey,
                discoverFilesystem: true,
                discoverRemote: false)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 変更パスから tenant_key（先頭セグメント）を推定する。
    /// レイアウトは <c>{modulesRoot}/{tenantKey}/{module}/...</c> を前提とする。
    /// </summary>
    internal static string? TryResolveTenantKeyFromPath(string modulesRoot, string fullPath)
    {
        var relative = Path.GetRelativePath(modulesRoot, fullPath);
        if (string.IsNullOrWhiteSpace(relative)
            || relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return null;
        }

        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        // tenantKey + moduleName 以上が必要
        return segments.Length >= 2 ? segments[0] : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Skipping module reload for unknown tenant directory '{TenantKey}' at {Path}")]
    public static partial void UnknownTenantDirectory(ILogger logger, string tenantKey, string path);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Ignoring module path outside tenant layout at {Path}; expected {{modulesRoot}}/{{tenantKey}}/{{module}}/")]
    public static partial void UnscopedModulePathIgnored(ILogger logger, string path);
}
