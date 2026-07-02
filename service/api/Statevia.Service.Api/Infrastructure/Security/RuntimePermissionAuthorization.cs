using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Infrastructure.Security;

/// <inheritdoc />
internal sealed class RuntimePermissionAuthorization : IRuntimePermissionAuthorization
{
    internal const string PermissionDeniedCode = "PERMISSION_DENIED";

    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public RuntimePermissionAuthorization(
        ITenantContextAccessor tenantContext,
        IPlatformDataAccess platformDataAccess)
    {
        _tenantContext = tenantContext;
        _platformDataAccess = platformDataAccess;
    }

    /// <inheritdoc />
    public async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            throw new ArgumentException("permissionKey is required.", nameof(permissionKey));

        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        if (_tenantContext.EffectivePermissionKeys is { } fixedKeys)
        {
            if (!fixedKeys.Contains(permissionKey))
                throw new ForbiddenException("Insufficient permission.", PermissionDeniedCode);
            return;
        }

        var expandedKeys = await _platformDataAccess
            .ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);

        if (!expandedKeys.Contains(permissionKey))
            throw new ForbiddenException("Insufficient permission.", PermissionDeniedCode);
    }
}
