using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Infrastructure.Modules;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Application.Actions.Modules;

/// <inheritdoc />
/// <remarks>
/// reload / 一覧はリクエストのテナント文脈にスコープする。
/// <see cref="ModuleHostOptions.OwnerTenantId"/> が設定されている場合のみ、固定 owner を優先する（レガシー）。
/// </remarks>
internal sealed class ModuleManagementService : IModuleManagementService
{
    private readonly ModuleHost _moduleHost;
    private readonly ModuleLoadCatalog _loadCatalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IOptions<ModuleHostOptions> _options;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="moduleHost">Module load ホスト。</param>
    /// <param name="loadCatalog">load 監査カタログ。</param>
    /// <param name="scopeFactory">DB 解決用スコープ。</param>
    /// <param name="tenantContext">現在リクエストのテナント文脈。</param>
    /// <param name="options">ModuleHost 設定。</param>
    public ModuleManagementService(
        ModuleHost moduleHost,
        ModuleLoadCatalog loadCatalog,
        IServiceScopeFactory scopeFactory,
        ITenantContextAccessor tenantContext,
        IOptions<ModuleHostOptions> options)
    {
        _moduleHost = moduleHost;
        _loadCatalog = loadCatalog;
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
        _options = options;
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var (ownerTenantId, tenantKey) = await ResolveReloadTargetAsync(cancellationToken).ConfigureAwait(false);
        await _moduleHost
            .LoadAsync(
                ownerTenantId,
                cancellationToken,
                filesystemTenantKey: tenantKey,
                discoverFilesystem: true,
                discoverRemote: true)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<AdminModuleListItemDto> ListModules()
    {
        var ownerTenantId = ResolveListOwnerTenantId();
        return _loadCatalog.GetRecords()
            .Where(record => string.Equals(record.OwnerTenantId, ownerTenantId, StringComparison.OrdinalIgnoreCase))
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
    }

    private async Task<(string OwnerTenantId, string TenantKey)> ResolveReloadTargetAsync(
        CancellationToken cancellationToken)
    {
        var configuredOwner = NormalizeOptional(_options.Value.OwnerTenantId);

        if (_tenantContext.IsResolved
            && _tenantContext.TenantId is { } tenantId
            && !string.IsNullOrWhiteSpace(_tenantContext.TenantKey))
        {
            var ownerTenantId = configuredOwner ?? tenantId.ToString("D");
            return (ownerTenantId, _tenantContext.TenantKey);
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

        var resolvedOwner = configuredOwner ?? tenant.TenantId.ToString("D");
        return (resolvedOwner, tenant.TenantKey);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string ResolveListOwnerTenantId()
    {
        var configured = _options.Value.OwnerTenantId;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        if (_tenantContext.IsResolved && _tenantContext.TenantId is { } tenantId)
        {
            return tenantId.ToString("D");
        }

        throw new InvalidOperationException(
            "Tenant context is not resolved. ListModules requires an authenticated tenant request.");
    }
}
