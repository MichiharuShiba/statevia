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
        const string username = "bootstrap-admin";
        const string password = "bootstrap-test-password";

        // Act
        var result = await bootstrap.CreateTenantAdminAsync(
            "default",
            username,
            password,
            displayName: null,
            skipIfExists: false,
            email: null,
            CancellationToken.None);

        // Assert
        Assert.True(result.Created);
        Assert.Equal(username, result.Username);
        Assert.Null(result.Email);

        var auth = new AuthService(
            new PlatformDataAccess(database.Factory, new DefaultIdGenerator()),
            CreateJwt(),
            new PasswordCredentialService());
        var login = await auth.LoginAsync(
            new LoginRequest
            {
                TenantKey = "default",
                Username = username,
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
        const string username = "existing";
        await SecurityTestSeed.SeedUserAsync(database, username, "seed-password", isTenantAdmin: true);

        // Act
        var result = await bootstrap.CreateTenantAdminAsync(
            "default",
            username,
            "other-password",
            displayName: null,
            skipIfExists: true,
            email: null,
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
                "admin",
                "password",
                displayName: null,
                skipIfExists: false,
                email: null,
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
        const string username = "dup";
        await SecurityTestSeed.SeedUserAsync(database, username, "seed-password", isTenantAdmin: true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.CreateTenantAdminAsync(
                "default",
                username,
                "other-password",
                displayName: null,
                skipIfExists: false,
                email: null,
                CancellationToken.None));
    }

    /// <summary>許可文字以外のユーザー名は拒否する。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_UsernameContainsAt_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            bootstrap.CreateTenantAdminAsync(
                "default",
                "admin@example.com",
                "password",
                displayName: null,
                skipIfExists: false,
                email: null,
                CancellationToken.None));
    }

    /// <summary>連絡先メールを指定すると結果に保持する。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_WithEmail_PersistsEmail()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);
        const string username = "mail-admin";
        const string email = "mail-admin@example.com";

        // Act
        var result = await bootstrap.CreateTenantAdminAsync(
            "default",
            username,
            "bootstrap-test-password",
            displayName: null,
            skipIfExists: false,
            email,
            CancellationToken.None);

        // Assert
        Assert.True(result.Created);
        Assert.Equal(email, result.Email);
    }

    /// <summary>同一テナントで連絡先メールが重複すると例外。</summary>
    [Fact]
    public async Task CreateTenantAdminAsync_DuplicateEmail_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = CreateBootstrap(database);
        const string email = "ops@example.com";
        await SecurityTestSeed.SeedUserAsync(database, email, "seed-password", isTenantAdmin: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.CreateTenantAdminAsync(
                "default",
                "other-admin",
                "password",
                displayName: null,
                skipIfExists: false,
                email,
                CancellationToken.None));
        Assert.Contains("Email already exists", ex.Message, StringComparison.Ordinal);
    }

    private static TenantAdminBootstrap CreateBootstrap(SqliteTestDatabase database) =>
        new(
            database.Factory,
            new PlatformDataAccess(database.Factory, new DefaultIdGenerator()),
            new PasswordCredentialService(),
            new DefaultIdGenerator());

    private static JwtTokenService CreateJwt()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new JwtAuthOptions { SigningKey = "test-signing-key-at-least-32-bytes-long!!" });
        return new JwtTokenService(options);
    }
}
