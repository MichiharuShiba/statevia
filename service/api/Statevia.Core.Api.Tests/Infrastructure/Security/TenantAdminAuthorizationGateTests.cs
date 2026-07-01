using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure.Security;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantAdminAuthorizationGate"/> の単体テスト。</summary>
public sealed class TenantAdminAuthorizationGateTests
{
    /// <summary>Principal 未解決は 401。</summary>
    [Fact]
    public async Task EnsureTenantAdminAsync_UnresolvedPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        var tenantContext = new SettableTenantContextAccessor();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            TenantAdminAuthorizationGate.EnsureTenantAdminAsync(
                tenantContext,
                new AlwaysAdminAuthorization(),
                CancellationToken.None));
    }

    /// <summary>非管理者は 403。</summary>
    [Fact]
    public async Task EnsureTenantAdminAsync_NonAdmin_ThrowsForbidden()
    {
        // Arrange
        var tenantContext = new SettableTenantContextAccessor();
        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = Guid.NewGuid() });

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            TenantAdminAuthorizationGate.EnsureTenantAdminAsync(
                tenantContext,
                new NeverAdminAuthorization(),
                CancellationToken.None));
    }

    /// <summary>テナント管理者は通過する。</summary>
    [Fact]
    public async Task EnsureTenantAdminAsync_Admin_Completes()
    {
        // Arrange
        var tenantContext = new SettableTenantContextAccessor();
        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = Guid.NewGuid() });

        // Act
        var exception = await Record.ExceptionAsync(() =>
            TenantAdminAuthorizationGate.EnsureTenantAdminAsync(
                tenantContext,
                new AlwaysAdminAuthorization(),
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private sealed class AlwaysAdminAuthorization : ITenantAdminAuthorization
    {
        /// <inheritdoc />
        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class NeverAdminAuthorization : ITenantAdminAuthorization
    {
        /// <inheritdoc />
        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
