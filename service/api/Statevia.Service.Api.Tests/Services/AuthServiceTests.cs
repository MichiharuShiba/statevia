using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="AuthService"/> の認証フロー。</summary>
public sealed class AuthServiceTests
{
    private static AuthService CreateAuthService(
        SqliteTestDatabase database,
        ILoginFailureLockStore? lockStore = null)
    {
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory, new DefaultIdGenerator());
        return new AuthService(
            platform,
            jwt,
            new PasswordCredentialService(),
            lockStore ?? new InMemoryLoginFailureLockStore(TimeProvider.System));
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
            Username = "admin",
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
            Username = "admin",
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
            Username = "admin",
            Password = "wrong"
        }, CancellationToken.None));
    }

    /// <summary>存在しないユーザーは 401 で、失敗カウントを増やさない。</summary>
    [Fact]
    public async Task LoginAsync_UnknownUser_ThrowsUnauthorizedWithoutRecording()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var store = new InMemoryLoginFailureLockStore(TimeProvider.System);
        var auth = CreateAuthService(database, store);

        // Act
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Username = "missing",
            Password = "password123"
        }, CancellationToken.None));

        // Assert
        Assert.Equal(0, store.TrackedCount);
        Assert.False(store.IsLocked("default", "missing"));
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
            Username = "admin",
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
            Username = "admin",
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
        Assert.Equal("me", response.Username);
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

    /// <summary>現行パスワード一致で本人ハッシュを更新できる。</summary>
    [Fact]
    public async Task ChangeOwnPasswordAsync_CurrentMatches_ReplacesHash()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "me@example.com", "password123");
        var auth = CreateAuthService(database);
        var hasher = new PasswordCredentialService();

        // Act
        await auth.ChangeOwnPasswordAsync(
            TestTenantIds.DefaultTenantId,
            principalId,
            new ChangeOwnPasswordRequest
            {
                CurrentPassword = "password123",
                NewPassword = "nextpass01"
            },
            CancellationToken.None);

        // Assert
        await using var db = database.Factory.CreateDbContext();
        var user = await db.Users.SingleAsync(row => row.Username == "me");
        Assert.True(hasher.VerifyPassword("nextpass01", user.PasswordHash));
        Assert.False(hasher.VerifyPassword("password123", user.PasswordHash));
    }

    /// <summary>現行パスワード不一致は 401 でハッシュは変わらない。</summary>
    [Fact]
    public async Task ChangeOwnPasswordAsync_WrongCurrent_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "me@example.com", "password123");
        var auth = CreateAuthService(database);
        var hasher = new PasswordCredentialService();
        string originalHash;
        await using (var db = database.Factory.CreateDbContext())
        {
            originalHash = (await db.Users.SingleAsync(row => row.Username == "me")).PasswordHash;
        }

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.ChangeOwnPasswordAsync(
                TestTenantIds.DefaultTenantId,
                principalId,
                new ChangeOwnPasswordRequest
                {
                    CurrentPassword = "wrong",
                    NewPassword = "nextpass01"
                },
                CancellationToken.None));

        await using (var db = database.Factory.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(row => row.Username == "me");
            Assert.Equal(originalHash, user.PasswordHash);
            Assert.True(hasher.VerifyPassword("password123", user.PasswordHash));
        }
    }

    /// <summary>無効ユーザーの残 JWT では本人更新できない。</summary>
    [Fact]
    public async Task ChangeOwnPasswordAsync_InactiveUser_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(
            database, "me@example.com", "password123", isActive: false);
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.ChangeOwnPasswordAsync(
                TestTenantIds.DefaultTenantId,
                principalId,
                new ChangeOwnPasswordRequest
                {
                    CurrentPassword = "password123",
                    NewPassword = "nextpass01"
                },
                CancellationToken.None));
    }

    /// <summary>API キー Principal の本人更新は 401。</summary>
    [Fact]
    public async Task ChangeOwnPasswordAsync_ApiKeyPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (principalId, _, _) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var auth = CreateAuthService(database);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.ChangeOwnPasswordAsync(
                TestTenantIds.DefaultTenantId,
                principalId,
                new ChangeOwnPasswordRequest
                {
                    CurrentPassword = "password123",
                    NewPassword = "nextpass01"
                },
                CancellationToken.None));
    }

    /// <summary>失敗が閾値に達すると正しいパスワードでも 401 になる。</summary>
    [Fact]
    public async Task LoginAsync_TooManyFailures_LocksEvenWithCorrectPassword()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var auth = CreateAuthService(database);
        var wrong = new LoginRequest
        {
            TenantKey = "default",
            Username = "admin",
            Password = "wrong"
        };

        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                auth.LoginAsync(wrong, CancellationToken.None));
        }

        // Act
        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest
            {
                TenantKey = "default",
                Username = "admin",
                Password = "password123"
            }, CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", locked.Code);
    }

    /// <summary>成功ログインのあと再度ログインできる。</summary>
    [Fact]
    public async Task LoginAsync_Success_ResetsFailureCount()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var auth = CreateAuthService(database);
        var wrong = new LoginRequest
        {
            TenantKey = "default",
            Username = "admin",
            Password = "wrong"
        };
        var correct = new LoginRequest
        {
            TenantKey = "default",
            Username = "admin",
            Password = "password123"
        };
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(wrong, CancellationToken.None));

        // Act
        var first = await auth.LoginAsync(correct, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(wrong, CancellationToken.None));
        var second = await auth.LoginAsync(correct, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(first.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(second.AccessToken));
    }
}
