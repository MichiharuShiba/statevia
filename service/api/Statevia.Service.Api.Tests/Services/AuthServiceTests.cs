using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
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
            lockStore ?? CreateLockStore(database));
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
        var store = CreateLockStore(database);
        var auth = CreateAuthService(database, store);

        // Act
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Username = "missing",
            Password = "password123"
        }, CancellationToken.None));

        // Assert
        await using var db = database.Factory.CreateDbContext();
        Assert.Equal(0, await db.LoginFailureLocks.CountAsync(row => row.Username == "missing"));
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

        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts; attempt++)
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

    /// <summary>同一 DB へ失敗を分散しても、合計が閾値に達したら全呼び出し元でロックする。</summary>
    [Fact]
    public async Task LoginAsync_FailuresSplitAcrossTwoCallers_LocksOnce()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var callerA = CreateAuthService(database, CreateLockStore(database));
        var callerB = CreateAuthService(database, CreateLockStore(database));
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
        var firstCallerFailures = LoginFailureLockStore.MaxFailedAttempts - 2;

        // Act
        for (var attempt = 0; attempt < firstCallerFailures; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                callerA.LoginAsync(wrong, CancellationToken.None));
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                callerB.LoginAsync(wrong, CancellationToken.None));
        }

        var lockedOnA = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            callerA.LoginAsync(correct, CancellationToken.None));
        var lockedOnB = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            callerB.LoginAsync(correct, CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", lockedOnA.Code);
        Assert.Equal("UNAUTHORIZED", lockedOnB.Code);
    }

    /// <summary>成功ログインで失敗ロックと attempts 行が DELETE される。</summary>
    [Fact]
    public async Task LoginAsync_Success_DeletesLockAndAttemptRows()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var auth = CreateAuthService(database);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest
            {
                TenantKey = "default",
                Username = "admin",
                Password = "wrong"
            }, CancellationToken.None));

        // Act
        await auth.LoginAsync(new LoginRequest
        {
            TenantKey = "default",
            Username = "admin",
            Password = "password123"
        }, CancellationToken.None);

        // Assert
        await using var db = database.Factory.CreateDbContext();
        Assert.Equal(0, await db.LoginFailureLocks.CountAsync(row => row.Username == "admin"));
        Assert.Equal(0, await db.LoginFailureAttempts.CountAsync(row => row.Username == "admin"));
    }

    /// <summary>IsLocked の読み取り失敗は 401 にせず、フィルターで 5xx になる。</summary>
    [Fact]
    public async Task LoginAsync_IsLockedFailure_MapsToInternalServerError()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password123");
        var auth = CreateAuthService(database, new ThrowingIsLockedStore());
        var filter = new ApiExceptionFilter(NullLogger<ApiExceptionFilter>.Instance);
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());

        // Act
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth.LoginAsync(new LoginRequest
            {
                TenantKey = "default",
                Username = "admin",
                Password = "password123"
            }, CancellationToken.None));
        var exceptionContext = new ExceptionContext(actionContext, []) { Exception = thrown };
        filter.OnException(exceptionContext);

        // Assert
        Assert.True(exceptionContext.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(exceptionContext.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    private static LoginFailureLockStore CreateLockStore(SqliteTestDatabase database) =>
        new(
            database.Factory,
            TimeProvider.System,
            new DefaultIdGenerator(),
            NullLogger<LoginFailureLockStore>.Instance);

    /// <summary>IsLocked が失敗したときの 5xx 写像を検証する。</summary>
    private sealed class ThrowingIsLockedStore : ILoginFailureLockStore
    {
        public bool IsLocked(string tenantKey, string username) =>
            throw new InvalidOperationException("lock store unavailable");

        public void RecordFailure(string tenantKey, string username) =>
            throw new NotSupportedException();

        public void Reset(string tenantKey, string username) =>
            throw new NotSupportedException();
    }
}
