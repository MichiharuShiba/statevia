namespace Statevia.Infrastructure.Security;

/// <summary>Module 管理 API の認可（テナント管理者または semantic permission）。</summary>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="tenantAdminAuthorization">テナント管理者判定。</param>
/// <param name="runtimeAuth">global permission 評価。</param>
internal sealed class ModuleManagementAuthorization(
    ITenantContextAccessor tenantContext,
    ITenantAdminAuthorization tenantAdminAuthorization,
    IRuntimePermissionAuthorization runtimeAuth) : IModuleManagementAuthorization
{
    /// <inheritdoc />
    public Task EnsureReloadAllowedAsync(CancellationToken cancellationToken) =>
        EnsureAdminOrPermissionAsync(WellKnownPermissionKeys.ModulesReload, cancellationToken);

    /// <inheritdoc />
    public Task EnsureReadAllowedAsync(CancellationToken cancellationToken) =>
        EnsureAdminOrPermissionAsync(WellKnownPermissionKeys.ModulesRead, cancellationToken);

    /// <summary>テナント管理者、または指定 permission のいずれかで通過させる。</summary>
    private async Task EnsureAdminOrPermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved || tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        if (await tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
            return;

        await runtimeAuth.EnsurePermissionAsync(permissionKey, cancellationToken).ConfigureAwait(false);
    }
}
