namespace Statevia.Core.Api.Infrastructure.Security;

/// <summary>テナント管理者判定（<c>users.is_tenant_admin</c>。グループのみに依存しない）。</summary>
internal interface ITenantAdminAuthorization
{
    /// <summary>Principal がテナント管理者か。</summary>
    Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class TenantAdminAuthorization : ITenantAdminAuthorization
{
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantAdminAuthorization(IPlatformDataAccess platformDataAccess) =>
        _platformDataAccess = platformDataAccess;

    /// <inheritdoc />
    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
        _platformDataAccess.IsTenantAdminAsync(principalId, cancellationToken);
}
