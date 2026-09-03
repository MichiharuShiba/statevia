namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="ModuleManagementAuthorization"/> の単体テスト。</summary>
public sealed class ModuleManagementAuthorizationTests
{
    /// <summary>Principal 未解決は 401。</summary>
    [Fact]
    public async Task EnsureReloadAllowedAsync_UnresolvedPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        var authorization = CreateAuthorization(isAdmin: false, permissionKeys: []);

        // Act
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authorization.EnsureReloadAllowedAsync(CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    /// <summary>テナント管理者は permission なしでも reload できる。</summary>
    [Fact]
    public async Task EnsureReloadAllowedAsync_TenantAdmin_Completes()
    {
        // Arrange
        var tenantContext = new SettableTenantContextAccessor();
        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = Guid.NewGuid() });
        var authorization = new ModuleManagementAuthorization(
            tenantContext,
            new StubTenantAdminAuthorization(isAdmin: true),
            new StubRuntimePermissionAuthorization([]));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureReloadAllowedAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>非管理者でも modules.reload があれば reload できる。</summary>
    [Fact]
    public async Task EnsureReloadAllowedAsync_ModulesReloadPermission_Completes()
    {
        // Arrange
        var authorization = CreateAuthorization(
            isAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.ModulesReload],
            principalId: Guid.NewGuid());

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureReloadAllowedAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>非管理者で modules.reload が無いと 403。</summary>
    [Fact]
    public async Task EnsureReloadAllowedAsync_MissingPermission_ThrowsForbidden()
    {
        // Arrange
        var authorization = CreateAuthorization(
            isAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.ExecutionsRead],
            principalId: Guid.NewGuid());

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsureReloadAllowedAsync(CancellationToken.None));

        // Assert
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, ex.Code);
    }

    /// <summary>非管理者でも modules.read があれば一覧できる。</summary>
    [Fact]
    public async Task EnsureReadAllowedAsync_ModulesReadPermission_Completes()
    {
        // Arrange
        var authorization = CreateAuthorization(
            isAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.ModulesRead],
            principalId: Guid.NewGuid());

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureReadAllowedAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private static ModuleManagementAuthorization CreateAuthorization(
        bool isAdmin,
        IReadOnlyList<string> permissionKeys,
        Guid? principalId = null)
    {
        var tenantContext = new SettableTenantContextAccessor();
        if (principalId is { } id)
        {
            tenantContext.Set(TestTenantIds.DefaultContext with
            {
                PrincipalId = id,
                EffectivePermissionKeys = permissionKeys.ToHashSet(StringComparer.Ordinal)
            });
        }

        return new ModuleManagementAuthorization(
            tenantContext,
            new StubTenantAdminAuthorization(isAdmin),
            new StubRuntimePermissionAuthorization(permissionKeys));
    }

    private sealed class StubTenantAdminAuthorization(bool isAdmin) : ITenantAdminAuthorization
    {
        /// <inheritdoc />
        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(isAdmin);
    }

    private sealed class StubRuntimePermissionAuthorization(IReadOnlyList<string> keys) : IRuntimePermissionAuthorization
    {
        /// <inheritdoc />
        public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
        {
            if (!keys.Contains(permissionKey, StringComparer.Ordinal))
                throw new ForbiddenException("Insufficient permission.", RuntimePermissionAuthorization.PermissionDeniedCode);
            return Task.CompletedTask;
        }
    }
}
