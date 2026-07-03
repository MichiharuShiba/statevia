using Microsoft.Extensions.Options;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Hosting;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Application.Actions.Modules;

/// <inheritdoc />
internal sealed class ModuleManagementService : IModuleManagementService
{
    private readonly ModuleHost _moduleHost;
    private readonly ModuleLoadCatalog _loadCatalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ModuleHostOptions> _options;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ModuleManagementService(
        ModuleHost moduleHost,
        ModuleLoadCatalog loadCatalog,
        IServiceScopeFactory scopeFactory,
        IOptions<ModuleHostOptions> options)
    {
        _moduleHost = moduleHost;
        _loadCatalog = loadCatalog;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var ownerTenantId = await ResolveOwnerTenantIdAsync(cancellationToken).ConfigureAwait(false);
        await _moduleHost.LoadAsync(ownerTenantId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<AdminModuleListItemDto> ListModules() =>
        _loadCatalog.GetRecords()
            .Select(record => new AdminModuleListItemDto
            {
                ModuleId = record.ModuleId,
                Name = record.Name,
                Version = record.Version,
                Status = record.Status.ToString(),
                Sha256 = record.Sha256,
                SourceLabel = record.SourceLabel,
                LoadedAtUtc = record.LoadedAtUtc,
                Message = record.Message,
                EntryAssemblyPath = record.EntryAssemblyPath,
            })
            .ToList();

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
            .FindTenantByKeyAsync(TenantRequestHeaders.DefaultTenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            throw new InvalidOperationException(
                "Default tenant not found. Ensure tenant bootstrap completed before module reload.");
        }

        return tenant.TenantId.ToString("D");
    }
}
