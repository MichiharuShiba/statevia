using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Publication;
using Statevia.Modules;

namespace Statevia.Core.Api.Application.Actions.Modules;

/// <summary>discover 結果の Action Module を load し Catalog へ登録する Core-API 固定ホスト。</summary>
internal sealed class ModuleHost
{
    private readonly IModuleSource _moduleSource;
    private readonly InMemoryActionCatalog _catalog;
    private readonly ModuleLoadCatalog _loadCatalog;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleHost> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, ModuleAssemblyLoadContext> _loadContexts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ModuleHost(
        IModuleSource moduleSource,
        InMemoryActionCatalog catalog,
        ModuleLoadCatalog loadCatalog,
        IServiceProvider serviceProvider,
        IOptions<ModuleHostOptions> options,
        ILogger<ModuleHost> logger)
    {
        _ = options;
        _moduleSource = moduleSource;
        _catalog = catalog;
        _loadCatalog = loadCatalog;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Module Source から discover し未 load の module を登録する。</summary>
    /// <param name="ownerTenantId">Module Action の所有者テナント ID（UUID 文字列）。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    public async Task LoadAsync(string ownerTenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTenantId);

        IReadOnlyList<DiscoveredModule> discovered;
        try
        {
            discovered = await _moduleSource.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ModuleHostLog.ModuleDiscoveryFailed(_logger, ex);
            return;
        }

        foreach (var module in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadDiscoveredModule(module, ownerTenantId);
        }
    }

    /// <summary>単一 module を load する（watcher 用）。</summary>
    /// <param name="discoveredModule">発見済み module。</param>
    /// <param name="ownerTenantId">所有者テナント ID。</param>
    public void LoadDiscoveredModule(DiscoveredModule discoveredModule, string ownerTenantId)
    {
        ArgumentNullException.ThrowIfNull(discoveredModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTenantId);

        lock (_sync)
        {
            if (_loadCatalog.IsLoaded(discoveredModule.EntryAssemblyPath))
            {
                return;
            }

            LoadDiscoveredModuleCore(discoveredModule, ownerTenantId);
        }
    }

    private void LoadDiscoveredModuleCore(DiscoveredModule discoveredModule, string ownerTenantId)
    {
        var sha256 = ComputeSha256Hex(discoveredModule.EntryAssemblyPath);
        var loadedAt = DateTimeOffset.UtcNow;

        try
        {
            var loadContext = new ModuleAssemblyLoadContext(discoveredModule.EntryAssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(discoveredModule.EntryAssemblyPath);
            var moduleType = FindActionModuleType(assembly);
            if (moduleType is null)
            {
                RecordFailure(
                    discoveredModule,
                    sha256,
                    loadedAt,
                    moduleId: discoveredModule.ModuleDirectoryName,
                    name: discoveredModule.ModuleDirectoryName,
                    version: "unknown",
                    message: "IActionModule implementation not found");
                return;
            }

            if (Activator.CreateInstance(moduleType) is not IActionModule actionModule)
            {
                RecordFailure(
                    discoveredModule,
                    sha256,
                    loadedAt,
                    moduleId: discoveredModule.ModuleDirectoryName,
                    name: discoveredModule.ModuleDirectoryName,
                    version: "unknown",
                    message: $"Failed to instantiate IActionModule type '{moduleType.FullName}'");
                return;
            }

            _loadContexts[discoveredModule.EntryAssemblyPath] = loadContext;

            var registrations = actionModule.GetActions(_serviceProvider).ToList();
            var moduleDescriptor = BuildModuleDescriptor(actionModule, registrations);

            var registeredCount = 0;
            var duplicateCount = 0;
            var skippedCount = 0;
            foreach (var registration in registrations)
            {
                switch (TryRegisterModuleAction(moduleDescriptor, registration, ownerTenantId))
                {
                    case ModuleActionRegisterOutcome.Registered:
                        registeredCount++;
                        break;
                    case ModuleActionRegisterOutcome.Duplicate:
                        duplicateCount++;
                        break;
                    case ModuleActionRegisterOutcome.Skipped:
                        skippedCount++;
                        break;
                }
            }

            var status = registeredCount switch
            {
                > 0 when duplicateCount > 0 || skippedCount > 0 => ModuleLoadStatus.Loaded,
                > 0 => ModuleLoadStatus.Loaded,
                0 when duplicateCount > 0 && skippedCount == 0 => ModuleLoadStatus.Duplicate,
                _ => ModuleLoadStatus.Skipped,
            };

            var message = registeredCount switch
            {
                > 0 => $"Registered {registeredCount} action(s)",
                0 when duplicateCount > 0 => "All actions were duplicates",
                _ => "No actions were registered from module",
            };

            if (status is ModuleLoadStatus.Skipped or ModuleLoadStatus.Duplicate)
            {
                ModuleHostLog.ModuleLoadedWithStatus(_logger, actionModule.ModuleId, status, message);
            }

            _loadCatalog.Upsert(new ModuleLoadRecord
            {
                ModuleId = actionModule.ModuleId,
                Name = actionModule.Name,
                Version = actionModule.Version,
                Status = status,
                Sha256 = sha256,
                SourceLabel = discoveredModule.SourceLabel,
                LoadedAtUtc = loadedAt,
                Message = message,
                EntryAssemblyPath = discoveredModule.EntryAssemblyPath,
                ModuleDescriptor = registeredCount > 0 ? moduleDescriptor : null,
            });

            if (registeredCount > 0)
            {
                ModuleHostLog.ModuleLoaded(
                    _logger,
                    actionModule.ModuleId,
                    actionModule.Version,
                    discoveredModule.EntryAssemblyPath,
                    sha256);
            }
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
            ModuleHostLog.ModuleLoadFailed(_logger, ex, discoveredModule.EntryAssemblyPath);

            RecordFailure(
                discoveredModule,
                sha256,
                loadedAt,
                moduleId: discoveredModule.ModuleDirectoryName,
                name: discoveredModule.ModuleDirectoryName,
                version: "unknown",
                message: ex.Message);
        }
    }

    private enum ModuleActionRegisterOutcome
    {
        Registered,
        Duplicate,
        Skipped,
    }

    private ModuleActionRegisterOutcome TryRegisterModuleAction(
        ModuleDescriptor moduleDescriptor,
        ModuleActionRegistration registration,
        string ownerTenantId)
    {
        if (string.IsNullOrWhiteSpace(registration.ActionId))
        {
            ModuleHostLog.ActionIdRequired(_logger, moduleDescriptor.ModuleId);
            return ModuleActionRegisterOutcome.Skipped;
        }

        var actionId = registration.ActionId.Trim();
        if (!actionId.StartsWith($"{moduleDescriptor.ModuleId}.", StringComparison.Ordinal))
        {
            ModuleHostLog.NamespaceConventionViolation(_logger, actionId, moduleDescriptor.ModuleId);
            return ModuleActionRegisterOutcome.Skipped;
        }

        if (_catalog.Exists(actionId))
        {
            ModuleHostLog.DuplicateActionSkipped(_logger, actionId, moduleDescriptor.ModuleId);
            return ModuleActionRegisterOutcome.Duplicate;
        }

        if (registration.ExecutorFactory is null)
        {
            ModuleHostLog.ExecutorFactoryRequired(_logger, actionId, moduleDescriptor.ModuleId);
            return ModuleActionRegisterOutcome.Skipped;
        }

        var descriptor = registration.Descriptor
            ?? CreateDefaultActionDescriptor(actionId, moduleDescriptor, ownerTenantId);

        try
        {
            ActionDescriptorInvariants.Validate(descriptor);
        }
        catch (ArgumentException ex)
        {
            ModuleHostLog.InvalidDescriptorSkipped(_logger, ex, actionId, moduleDescriptor.ModuleId);
            return ModuleActionRegisterOutcome.Skipped;
        }

        if (registration.Publication is not null)
        {
            try
            {
                ActionUiMetadataValidator.Validate(actionId, registration.Publication);
            }
            catch (ArgumentException ex)
            {
                ModuleHostLog.InvalidPublicationSkipped(_logger, ex, actionId, moduleDescriptor.ModuleId);
                return ModuleActionRegisterOutcome.Skipped;
            }
        }

        var entry = new ActionCatalogEntry(InProcessFactory: registration.ExecutorFactory);
        if (registration.Publication is not null)
        {
            _catalog.Register(descriptor, entry, registration.Publication);
        }
        else
        {
            _catalog.Register(descriptor, entry);
            if (descriptor.Visibility != ActionVisibility.Builtin)
            {
                ModuleHostLog.PublicationMissing(_logger, actionId, moduleDescriptor.ModuleId);
            }
        }

        return ModuleActionRegisterOutcome.Registered;
    }

    private void RecordFailure(
        DiscoveredModule discoveredModule,
        string sha256,
        DateTimeOffset loadedAt,
        string moduleId,
        string name,
        string version,
        string message)
    {
        _loadCatalog.Upsert(new ModuleLoadRecord
        {
            ModuleId = moduleId,
            Name = name,
            Version = version,
            Status = ModuleLoadStatus.Failed,
            Sha256 = sha256,
            SourceLabel = discoveredModule.SourceLabel,
            LoadedAtUtc = loadedAt,
            Message = message,
            EntryAssemblyPath = discoveredModule.EntryAssemblyPath,
        });
    }

    private static ModuleDescriptor BuildModuleDescriptor(
        IActionModule actionModule,
        IReadOnlyList<ModuleActionRegistration> registrations)
    {
        var actionIds = registrations
            .Select(registration => registration.ActionId.Trim())
            .Where(actionId => !string.IsNullOrWhiteSpace(actionId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(actionId => actionId, StringComparer.Ordinal)
            .ToList();

        return new ModuleDescriptor(
            actionModule.ModuleId,
            actionModule.Version,
            new ActionPublisher(actionModule.ModuleId, actionModule.Name),
            ActionTrustLevel.Community,
            ActionSourceKind.Filesystem,
            Signature: null,
            actionIds);
    }

    private static ActionDescriptor CreateDefaultActionDescriptor(
        string actionId,
        ModuleDescriptor moduleDescriptor,
        string ownerTenantId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = moduleDescriptor.ModuleId,
            Version = moduleDescriptor.Version,
            TrustLevel = moduleDescriptor.TrustLevel,
            Source = moduleDescriptor.Source,
            OwnerTenantId = ownerTenantId,
            Visibility = ActionVisibility.Tenant,
            Publisher = moduleDescriptor.Publisher,
            Signature = moduleDescriptor.Signature,
            ExecutionHints = new ActionExecutionHints
            {
                PreferredMode = ActionExecutionMode.InProcess,
            },
        };

    private static Type? FindActionModuleType(Assembly assembly) =>
        assembly.GetTypes()
            .FirstOrDefault(type =>
                typeof(IActionModule).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false });

    private static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}

internal static partial class ModuleHostLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Module discovery failed")]
    public static partial void ModuleDiscoveryFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Module {ModuleId} loaded with status {Status}: {Message}")]
    public static partial void ModuleLoadedWithStatus(ILogger logger, string moduleId, ModuleLoadStatus status, string message);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Loaded module {ModuleId} v{Version} from {EntryAssemblyPath} (SHA256={Sha256})")]
    public static partial void ModuleLoaded(ILogger logger, string moduleId, string version, string entryAssemblyPath, string sha256);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to load module from {EntryAssemblyPath}")]
    public static partial void ModuleLoadFailed(ILogger logger, Exception exception, string entryAssemblyPath);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Skipping action from module {ModuleId}: ActionId is required")]
    public static partial void ActionIdRequired(ILogger logger, string moduleId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Skipping action '{ActionId}' from module {ModuleId}: namespace convention violation")]
    public static partial void NamespaceConventionViolation(ILogger logger, string actionId, string moduleId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Skipping duplicate action '{ActionId}' from module {ModuleId}")]
    public static partial void DuplicateActionSkipped(ILogger logger, string actionId, string moduleId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "Skipping action '{ActionId}' from module {ModuleId}: ExecutorFactory is required")]
    public static partial void ExecutorFactoryRequired(ILogger logger, string actionId, string moduleId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Skipping action '{ActionId}' from module {ModuleId}: invalid descriptor")]
    public static partial void InvalidDescriptorSkipped(ILogger logger, Exception exception, string actionId, string moduleId);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Skipping action '{ActionId}' from module {ModuleId}: invalid publication")]
    public static partial void InvalidPublicationSkipped(ILogger logger, Exception exception, string actionId, string moduleId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Registered action '{ActionId}' from module {ModuleId} without ActionPublication; compile will require schema")]
    public static partial void PublicationMissing(ILogger logger, string actionId, string moduleId);
}
