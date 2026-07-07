using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Configuration;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// 起動時に既定テナント（<c>tenant_key = default</c>）と権限カタログを保証する。
/// Development かつ設定有効時は初回管理者も <see cref="TenantAdminBootstrap"/> で作成する。
/// </summary>
internal sealed class TenantBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly DevAdminBootstrapOptions _devAdminOptions;
    private readonly ILogger<TenantBootstrapHostedService> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<DevAdminBootstrapOptions> devAdminOptions,
        ILogger<TenantBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _devAdminOptions = devAdminOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDataAccess = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platformDataAccess.EnsureDefaultTenantAsync(cancellationToken).ConfigureAwait(false);
        await platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);

        if (!_environment.IsDevelopment() || !_devAdminOptions.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_devAdminOptions.Email)
            || string.IsNullOrWhiteSpace(_devAdminOptions.Password)
            || string.IsNullOrWhiteSpace(_devAdminOptions.TenantKey))
        {
            _logger.LogWarning(
                "Dev admin bootstrap is enabled but TenantKey, Email, or Password is missing; skipping.");
            return;
        }

        var tenantAdminBootstrap = scope.ServiceProvider.GetRequiredService<TenantAdminBootstrap>();
        var result = await tenantAdminBootstrap.CreateTenantAdminAsync(
            _devAdminOptions.TenantKey,
            _devAdminOptions.Email,
            _devAdminOptions.Password,
            _devAdminOptions.DisplayName,
            skipIfExists: true,
            cancellationToken).ConfigureAwait(false);

        if (result.Created)
        {
            _logger.LogInformation(
                "Development admin user created for tenant {TenantKey} (email: {Email}).",
                result.TenantKey,
                result.Email);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
