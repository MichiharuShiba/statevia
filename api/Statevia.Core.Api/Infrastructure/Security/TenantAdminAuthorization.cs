using Statevia.Core.Api.Abstractions.Security;

namespace Statevia.Core.Api.Infrastructure.Security;

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
