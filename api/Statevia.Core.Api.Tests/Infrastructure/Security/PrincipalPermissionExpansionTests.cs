using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;
namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary>権限展開とテナント管理者判定の検証。</summary>
public sealed class PrincipalPermissionExpansionTests
{
    /// <summary>権限カタログは semantic key と表示メタが分離されている。</summary>
    [Fact]
    public void PermissionCatalog_Entries_SeparateSemanticKeyFromDisplayMeta()
    {
        // Arrange & Act
        var entry = PermissionCatalog.Entries.First(e => e.PermissionKey == WellKnownPermissionKeys.ExecutionsRead);

        // Assert
        Assert.Equal(WellKnownPermissionKeys.ExecutionsRead, entry.PermissionKey);
        Assert.False(string.IsNullOrWhiteSpace(entry.DisplayLabel));
        Assert.NotEqual(entry.PermissionKey, entry.DisplayLabel);
        Assert.False(string.IsNullOrWhiteSpace(entry.DisplayKey));
    }

    /// <summary>テナント管理者はグループ未所属でも全カタログ権限を展開する。</summary>
    [Fact]
    public async Task ExpandPrincipalPermissionKeysAsync_TenantAdminWithoutGroup_ReturnsAllCatalogKeys()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password", isTenantAdmin: true);
        var platform = new PlatformDataAccess(database.Factory);
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);

        // Act
        var keys = await platform.ExpandPrincipalPermissionKeysAsync(principalId, CancellationToken.None);

        // Assert
        Assert.Contains(WellKnownPermissionKeys.ExecutionsRead, keys);
        Assert.Contains(WellKnownPermissionKeys.TenantAdmin, keys);
    }

    /// <summary>非管理者はグループ付与分のみ展開する。</summary>
    [Fact]
    public async Task ExpandPrincipalPermissionKeysAsync_NonAdminWithGroup_ReturnsGroupPermissionsOnly()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var platform = new PlatformDataAccess(database.Factory);
        var lookup = await platform.FindApiKeyCredentialAsync(
            PasswordCredentialService.ApiKeyPrefix(plainKey),
            PasswordCredentialService.HashApiKey(plainKey),
            CancellationToken.None);

        // Act
        var keys = await platform.ExpandPrincipalPermissionKeysAsync(lookup!.Principal.PrincipalId, CancellationToken.None);

        // Assert
        Assert.Single(keys);
        Assert.Contains(WellKnownPermissionKeys.ExecutionsRead, keys);
        Assert.DoesNotContain(WellKnownPermissionKeys.TenantAdmin, keys);
    }

    /// <summary><c>is_tenant_admin</c> はグループとは独立して true になる。</summary>
    [Fact]
    public async Task IsTenantAdminAsync_AdminUserWithoutGroup_ReturnsTrue()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "admin-only@example.com", "password", isTenantAdmin: true);
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var isAdmin = await platform.IsTenantAdminAsync(principalId, CancellationToken.None);

        // Assert
        Assert.True(isAdmin);
    }

    /// <summary>カタログ投入は二回目以降も重複行を作らない。</summary>
    [Fact]
    public async Task EnsurePermissionCatalogAsync_SecondCall_DoesNotDuplicateRows()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);

        // Assert
        await using var db = database.Factory.CreateDbContext();
        var count = await db.PermissionDefinitions
            .IgnoreQueryFilters()
            .CountAsync();
        Assert.Equal(PermissionCatalog.Entries.Count, count);
    }

    /// <summary>User に紐づかない Principal はグループ展開できない。</summary>
    [Fact]
    public async Task ExpandPrincipalPermissionKeysAsync_PrincipalWithoutUser_ReturnsEmpty()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var db = database.Factory.CreateDbContext())
        {
            db.Principals.Add(new PrincipalRow
            {
                PrincipalId = principalId,
                TenantId = TestTenantIds.DefaultInternalId,
                PrincipalScope = PrincipalScope.Tenant,
                PrincipalType = PrincipalType.ServiceAccount,
                DisplayName = "orphan",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var keys = await platform.ExpandPrincipalPermissionKeysAsync(principalId, CancellationToken.None);

        // Assert
        Assert.Empty(keys);
    }

    /// <summary>非管理者は <c>is_tenant_admin</c> false。</summary>
    [Fact]
    public async Task IsTenantAdminAsync_NonAdminUser_ReturnsFalse()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "member@example.com", "password", isTenantAdmin: false);
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var isAdmin = await platform.IsTenantAdminAsync(principalId, CancellationToken.None);

        // Assert
        Assert.False(isAdmin);
    }
}
