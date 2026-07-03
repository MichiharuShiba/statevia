using Statevia.Service.Api.Contracts.Auth;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantAdminBootstrap"/> の検証。</summary>
public sealed class TenantAdminBootstrapTests
{
    /// <summary>既定テナントに管理者を作成しログインできる。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_NewUser_CanLogin()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);
        const string email = "bootstrap-admin@example.com";
        const string password = "bootstrap-test-password";

        // Act
        var result = await bootstrap.CreateTenantAdminAsync(
            "default",
            email,
            password,
            displayName: null,
            skipIfExists: false,
            CancellationToken.None);

        // Assert
        Assert.True(result.Created);
        Assert.Equal(email, result.Email);

        var auth = new AuthService(
            new PlatformDataAccess(database.Factory),
            CreateJwt(),
            new PasswordCredentialService());
        var login = await auth.LoginAsync(
            new LoginRequest
            {
                TenantKey = "default",
                Email = email,
                Password = password
            },
            CancellationToken.None);

        Assert.NotNull(login);
        Assert.Equal(result.PrincipalId, login.PrincipalId);
        Assert.True(login.AccessToken.Length > 0);
    }

    /// <summary>既存ユーザーがいる場合 skipIfExists で再作成しない。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_ExistingUser_SkipIfExists_DoesNotDuplicate()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);
        const string email = "existing@example.com";
        await SecurityTestSeed.SeedUserAsync(database, email, "seed-password", isTenantAdmin: true);

        // Act
        var result = await bootstrap.CreateTenantAdminAsync(
            "default",
            email,
            "other-password",
            displayName: null,
            skipIfExists: true,
            CancellationToken.None);

        // Assert
        Assert.False(result.Created);
    }

    /// <summary>存在しないテナントは例外。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_UnknownTenant_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.CreateTenantAdminAsync(
                "missing-tenant",
                "admin@example.com",
                "password",
                displayName: null,
                skipIfExists: false,
                CancellationToken.None));
        Assert.Contains("missing-tenant", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>既存ユーザーで skipIfExists なしは例外。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_ExistingUser_WithoutSkip_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);
        const string email = "dup@example.com";
        await SecurityTestSeed.SeedUserAsync(database, email, "seed-password", isTenantAdmin: true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.CreateTenantAdminAsync(
                "default",
                email,
                "other-password",
                displayName: null,
                skipIfExists: false,
                CancellationToken.None));
    }

    private static TenantAdminBootstrap CreateBootstrap(SqliteTestDatabase database) =>
        new(
            database.Factory,
            new PlatformDataAccess(database.Factory),
            new PasswordCredentialService());

    private static JwtTokenService CreateJwt()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new JwtAuthOptions { SigningKey = "test-signing-key-at-least-32-bytes-long!!" });
        return new JwtTokenService(options);
    }
}
