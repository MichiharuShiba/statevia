using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Hosting;

/// <summary>起動時に既定テナント（<c>tenant_key = default</c>）が存在することを保証する。</summary>
internal sealed class TenantBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="scopeFactory">起動時スコープ生成用。</param>
    public TenantBootstrapHostedService(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDataAccess = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platformDataAccess.EnsureDefaultTenantAsync(cancellationToken).ConfigureAwait(false);
        await platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
