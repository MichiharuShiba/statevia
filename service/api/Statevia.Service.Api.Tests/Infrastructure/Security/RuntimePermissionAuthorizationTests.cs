using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="RuntimePermissionAuthorization"/> の検証。</summary>
public sealed class RuntimePermissionAuthorizationTests
{
    /// <summary>JWT 経路で permission が不足すると Forbidden。</summary>
    [Fact]
    public async Task EnsurePermissionAsync_JwtWithoutPermission_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(
            database,
            "reader@example.com",
            "password",
            isTenantAdmin: false);

        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var authorization = new RuntimePermissionAuthorization(
            accessor,
            new PlatformDataAccess(database.Factory));

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsurePermissionAsync(WellKnownPermissionKeys.DefinitionsRead, CancellationToken.None));

        // Assert
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, ex.Code);
    }

    /// <summary>permissionKey 未指定は ArgumentException。</summary>
    [Fact]
    public async Task EnsurePermissionAsync_EmptyPermissionKey_ThrowsArgumentException()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = Guid.NewGuid() });
        var authorization = new RuntimePermissionAuthorization(
            accessor,
            new PlatformDataAccess(database.Factory));

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            authorization.EnsurePermissionAsync("  ", CancellationToken.None));

        // Assert
        Assert.Equal("permissionKey", ex.ParamName);
    }

    /// <summary>Principal 未解決は Unauthorized。</summary>
    [Fact]
    public async Task EnsurePermissionAsync_MissingPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext);
        var authorization = new RuntimePermissionAuthorization(
            accessor,
            new PlatformDataAccess(database.Factory));

        // Act
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authorization.EnsurePermissionAsync(WellKnownPermissionKeys.DefinitionsRead, CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    /// <summary>JWT 経路で permission があれば成功する。</summary>
    [Fact]
    public async Task EnsurePermissionAsync_JwtWithPermission_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "def-reader@example.com",
            "password",
            [WellKnownPermissionKeys.DefinitionsRead]);

        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var authorization = new RuntimePermissionAuthorization(
            accessor,
            new PlatformDataAccess(database.Factory));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsurePermissionAsync(WellKnownPermissionKeys.DefinitionsRead, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>API キー経路では交差済み scopes のみ評価する。</summary>
    [Fact]
    public async Task EnsurePermissionAsync_ApiKeyEffectiveScopes_UsesFixedSet()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedApiKeyAsync(
            database,
            allowedScopesJson: "[\"executions.read\"]");
        var effectiveScopes = new HashSet<string>(StringComparer.Ordinal)
        {
            WellKnownPermissionKeys.ExecutionsRead
        };

        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with
        {
            PrincipalId = principalId.PrincipalId,
            EffectivePermissionKeys = effectiveScopes
        });
        var authorization = new RuntimePermissionAuthorization(
            accessor,
            new PlatformDataAccess(database.Factory));

        // Act
        var readException = await Record.ExceptionAsync(() =>
            authorization.EnsurePermissionAsync(WellKnownPermissionKeys.ExecutionsRead, CancellationToken.None));
        var writeException = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsurePermissionAsync(WellKnownPermissionKeys.ExecutionsWrite, CancellationToken.None));

        // Assert
        Assert.Null(readException);
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, writeException.Code);
    }
}
