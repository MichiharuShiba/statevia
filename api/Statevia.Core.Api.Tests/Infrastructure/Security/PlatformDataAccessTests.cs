using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="PlatformDataAccess"/> の Platform 専用 lookup。</summary>
public sealed class PlatformDataAccessTests
{
    /// <summary>既存 tenant_key でテナント行を取得できる。</summary>
    [Fact]
    public async Task FindTenantByKeyAsync_ExistingKey_ReturnsTenant()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var tenant = await platform.FindTenantByKeyAsync("default", CancellationToken.None);

        // Assert
        Assert.NotNull(tenant);
        Assert.Equal(TestTenantIds.DefaultTenantId, tenant.TenantId);
    }

    /// <summary>存在しない tenant_key は null。</summary>
    [Fact]
    public async Task FindTenantByKeyAsync_MissingKey_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var tenant = await platform.FindTenantByKeyAsync("missing", CancellationToken.None);

        // Assert
        Assert.Null(tenant);
    }

    /// <summary>ログイン資格情報を tenant / user / principal で解決できる。</summary>
    [Fact]
    public async Task FindLoginCredentialAsync_ValidUser_ReturnsLookup()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "user@example.com", "password123");
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var lookup = await platform.FindLoginCredentialAsync("default", "user@example.com", CancellationToken.None);

        // Assert
        Assert.NotNull(lookup);
        Assert.Equal("user@example.com", lookup.User.Email);
        Assert.Equal(TestTenantIds.DefaultTenantId, lookup.Tenant.TenantId);
        Assert.True(lookup.Principal.IsActive);
    }

    /// <summary>非アクティブユーザーは lookup されない。</summary>
    [Fact]
    public async Task FindLoginCredentialAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await SecurityTestSeed.SeedUserAsync(database, "inactive@example.com", "password123", isActive: false);
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var lookup = await platform.FindLoginCredentialAsync("default", "inactive@example.com", CancellationToken.None);

        // Assert
        Assert.Null(lookup);
    }

    /// <summary>Principal ID でユーザー情報を取得できる。</summary>
    [Fact]
    public async Task FindUserPrincipalAsync_ValidPrincipal_ReturnsLookup()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "me@example.com", "password123");
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var lookup = await platform.FindUserPrincipalAsync(
            TestTenantIds.DefaultTenantId,
            principalId,
            CancellationToken.None);

        // Assert
        Assert.NotNull(lookup);
        Assert.Equal("me@example.com", lookup.User.Email);
        Assert.Equal(principalId, lookup.Principal.PrincipalId);
    }

    /// <summary>既定テナントが無い場合に作成する。</summary>
    [Fact]
    public async Task EnsureDefaultTenantAsync_WhenMissing_CreatesDefaultTenant()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var db = database.Factory.CreateDbContext())
        {
            var tenants = await db.Tenants.IgnoreQueryFilters().ToListAsync();
            db.Tenants.RemoveRange(tenants);
            await db.SaveChangesAsync();
        }

        var platform = new PlatformDataAccess(database.Factory);

        // Act
        await platform.EnsureDefaultTenantAsync(CancellationToken.None);
        var tenant = await platform.FindTenantByKeyAsync("default", CancellationToken.None);

        // Assert
        Assert.NotNull(tenant);
        Assert.Equal(TenantLifecycle.Active, tenant.Lifecycle);
    }

    /// <summary>API キー prefix + hash で資格情報を解決できる。</summary>
    [Fact]
    public async Task FindApiKeyCredentialAsync_ValidKey_ReturnsLookup()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (principalId, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var platform = new PlatformDataAccess(database.Factory);
        var prefix = PasswordCredentialService.ApiKeyPrefix(plainKey);
        var hash = PasswordCredentialService.HashApiKey(plainKey);

        // Act
        var lookup = await platform.FindApiKeyCredentialAsync(prefix, hash, CancellationToken.None);

        // Assert
        Assert.NotNull(lookup);
        Assert.Equal(principalId, lookup.Principal.PrincipalId);
        Assert.Equal(TestTenantIds.DefaultTenantId, lookup.Tenant.TenantId);
    }

    /// <summary>ハッシュ不一致は null。</summary>
    [Fact]
    public async Task FindApiKeyCredentialAsync_WrongHash_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var platform = new PlatformDataAccess(database.Factory);
        var prefix = PasswordCredentialService.ApiKeyPrefix(plainKey);

        // Act
        var lookup = await platform.FindApiKeyCredentialAsync(prefix, "wrong-hash", CancellationToken.None);

        // Assert
        Assert.Null(lookup);
    }

    /// <summary>存在する API キーの last_used_at を更新する。</summary>
    [Fact]
    public async Task TouchApiKeyLastUsedAsync_ExistingKey_SetsLastUsedAt()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, apiKeyId, _) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        await platform.TouchApiKeyLastUsedAsync(apiKeyId, CancellationToken.None);

        // Assert
        await using var db = database.Factory.CreateDbContext();
        var row = await db.ApiKeys.IgnoreQueryFilters().SingleAsync(k => k.ApiKeyId == apiKeyId);
        Assert.NotNull(row.LastUsedAt);
    }

    /// <summary>存在しない API キー ID は no-op。</summary>
    [Fact]
    public async Task TouchApiKeyLastUsedAsync_MissingKey_DoesNotThrow()
    {
        // Arrange
        var missingApiKeyId = Guid.NewGuid();
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            platform.TouchApiKeyLastUsedAsync(missingApiKeyId, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        await using var db = database.Factory.CreateDbContext();
        Assert.False(await db.ApiKeys.IgnoreQueryFilters().AnyAsync(k => k.ApiKeyId == missingApiKeyId));
    }
}
