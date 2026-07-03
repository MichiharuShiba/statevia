using Microsoft.EntityFrameworkCore;

using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

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

    /// <summary>Principal ID で Principal 行を取得できる。</summary>
    [Fact]
    public async Task FindPrincipalAsync_Existing_ReturnsPrincipal()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "principal@example.com", "password");
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var principal = await platform.FindPrincipalAsync(principalId, CancellationToken.None);

        // Assert
        Assert.NotNull(principal);
        Assert.Equal(principalId, principal.PrincipalId);
    }

    /// <summary>存在しない Principal ID は null。</summary>
    [Fact]
    public async Task FindPrincipalAsync_Missing_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var principal = await platform.FindPrincipalAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>ユーザーの所属グループを ID と名称で返す。</summary>
    [Fact]
    public async Task GetGroupSnapshotsForPrincipalAsync_UserWithGroup_ReturnsSnapshots()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "group-user@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsRead]);
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var snapshots = await platform.GetGroupSnapshotsForPrincipalAsync(principalId, CancellationToken.None);

        // Assert
        Assert.NotEmpty(snapshots);
        Assert.All(snapshots, snapshot => Assert.False(string.IsNullOrWhiteSpace(snapshot.Name)));
    }

    /// <summary>execution_id からテナント境界を解決する。</summary>
    [Fact]
    public async Task FindExecutionTenantAsync_ReturnsTenant_ForExistingExecution()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executionId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var seed = database.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.T1TenantId, "t1", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.T1TenantId,
                definitionId,
                "wf-tenant-lookup",
                projectId,
                versionId: versionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = versionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await seed.SaveChangesAsync();
        }

        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var lookup = await platform.FindExecutionTenantAsync(executionId, CancellationToken.None);

        // Assert
        Assert.NotNull(lookup);
        Assert.Equal(TestTenantIds.T1TenantId, lookup!.TenantId);
        Assert.Equal("t1", lookup.TenantKey);
    }

    /// <summary>存在しない execution は null。</summary>
    [Fact]
    public async Task FindExecutionTenantAsync_ReturnsNull_WhenExecutionMissing()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory);

        // Act
        var lookup = await platform.FindExecutionTenantAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(lookup);
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
