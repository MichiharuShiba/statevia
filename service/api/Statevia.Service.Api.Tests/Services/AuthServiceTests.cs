using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="AuthService"/> の認証フロー。</summary>
public sealed class AuthServiceTests
{
    private static AuthService CreateAuthService(SqliteTestDatabase database)
    {
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        return new AuthService(platform, jwt, new PasswordCredentialService());
    }

    /// <summary>正しい資格情報で JWT が発行される。</summary>
    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");

        var auth = CreateAuthService(database);

        // Act
        var response = await auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Email = "admin@example.com",
            Password = "password123"
        }, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal("default", response.TenantKey);
        Assert.Equal(TestTenantIds.DefaultTenantId, response.TenantId);
    }

    /// <summary>空の資格情報は 401。</summary>
    [Fact]
    public async Task LoginAsync_EmptyCredentials_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "",
            Email = "admin@example.com",
            Password = "password123"
        }, CancellationToken.None));
    }

    /// <summary>パスワード不一致は 401。</summary>
    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Email = "admin@example.com",
            Password = "wrong"
        }, CancellationToken.None));
    }

    /// <summary>存在しないユーザーは 401。</summary>
    [Fact]
    public async Task LoginAsync_UnknownUser_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Email = "missing@example.com",
            Password = "password123"
        }, CancellationToken.None));
    }

    /// <summary>停止テナントは 403。</summary>
    [Fact]
    public async Task LoginAsync_SuspendedTenant_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");

        await using (var db = database.Factory.CreateDbContext())
        {
            var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.TenantKey == "default");
            tenant.Lifecycle = TenantLifecycle.Suspended;
            await db.SaveChangesAsync();
        }

        var auth = CreateAuthService(database);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Email = "admin@example.com",
            Password = "password123"
        }, CancellationToken.None));

        Assert.Equal("TENANT_SUSPENDED", ex.Code);
    }

    /// <summary>アーカイブ済みテナントは 403。</summary>
    [Fact]
    public async Task LoginAsync_ArchivedTenant_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");

        await using (var db = database.Factory.CreateDbContext())
        {
            var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.TenantKey == "default");
            tenant.Lifecycle = TenantLifecycle.Archived;
            await db.SaveChangesAsync();
        }

        var auth = CreateAuthService(database);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Email = "admin@example.com",
            Password = "password123"
        }, CancellationToken.None));

        Assert.Equal("TENANT_ARCHIVED", ex.Code);
    }

    /// <summary>GetMeAsync は認証済み Principal 情報を返す。</summary>
    [Fact]
    public async Task GetMeAsync_ValidPrincipal_ReturnsProfile()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "me@example.com", "password123");
        var auth = CreateAuthService(database);

        // Act
        var response = await auth.GetMeAsync(TestTenantIds.DefaultTenantId, principalId, CancellationToken.None);

        // Assert
        Assert.Equal("me@example.com", response.Email);
        Assert.Equal("default", response.TenantKey);
        Assert.Equal(principalId, response.PrincipalId);
        Assert.True(response.IsTenantAdmin);
    }

    /// <summary>存在しない Principal は 401。</summary>
    [Fact]
    public async Task GetMeAsync_UnknownPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.GetMeAsync(TestTenantIds.DefaultTenantId, Guid.NewGuid(), CancellationToken.None));
    }
}
