
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="TenantAdministrationService"/> の管理者 CRUD。</summary>
public sealed class TenantAdministrationServiceTests
{
    private static TenantAdministrationService CreateService(
        SqliteTestDatabase database,
        SettableTenantContextAccessor tenantContext,
        Guid callerPrincipalId,
        TenantContextState? tenant = null)
    {
        tenantContext.Set((tenant ?? TestTenantIds.DefaultContext) with { PrincipalId = callerPrincipalId });
        return new TenantAdministrationService(
            database.Factory,
            tenantContext,
            new TenantAdminAuthorization(new PlatformDataAccess(database.Factory, new DefaultIdGenerator())),
            new PasswordCredentialService(),
            new DefaultIdGenerator());
    }

    /// <summary>非管理者はユーザー一覧を拒否される。</summary>
    [Fact]
    public async Task ListUsersAsync_NonAdmin_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var memberId = await SecurityTestSeed.SeedUserAsync(database, "member@example.com", "password", isTenantAdmin: false);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, memberId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ListUsersAsync(memberId, CancellationToken.None));
    }

    /// <summary>管理者はユーザーを作成できる。</summary>
    [Fact]
    public async Task CreateUserAsync_Admin_CreatesUser()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var created = await service.CreateUserAsync(
            adminId,
            new CreateAdminUserRequest
            {
                Username = "new-user",
                Email = "new-user@example.com",
                Password = "initial-password",
                DisplayName = "New User"
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("new-user", created.Username);
        Assert.Equal("new-user@example.com", created.Email);
        Assert.Equal("New User", created.DisplayName);
        Assert.True(created.IsActive);
        Assert.False(created.IsTenantAdmin);
    }

    /// <summary>管理者はユーザーを無効化できる。</summary>
    [Fact]
    public async Task UpdateUserAsync_Admin_DisablesUser()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        var created = await service.CreateUserAsync(
            adminId,
            new CreateAdminUserRequest
            {
                Username = "disable-me",
                Email = "disable-me@example.com",
                Password = "initial-password"
            },
            CancellationToken.None);

        // Act
        var updated = await service.UpdateUserAsync(
            adminId,
            created.UserId,
            new UpdateAdminUserRequest { IsActive = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.IsActive);
    }

    /// <summary>グループ権限から tenant.admin は除外される。</summary>
    [Fact]
    public async Task SetGroupPermissionsAsync_FiltersTenantAdminKey()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory, new DefaultIdGenerator());
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin-groups@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        var group = await service.CreateGroupAsync(
            adminId,
            new CreateAdminGroupRequest { Name = $"ops-{Guid.NewGuid():N}" },
            CancellationToken.None);

        // Act
        var updated = await service.SetGroupPermissionsAsync(
            adminId,
            group.GroupId,
            new SetAdminGroupPermissionsRequest
            {
                PermissionKeys = [WellKnownPermissionKeys.TenantAdmin, WellKnownPermissionKeys.DefinitionsRead]
            },
            CancellationToken.None);

        // Assert
        Assert.DoesNotContain(WellKnownPermissionKeys.TenantAdmin, updated.PermissionKeys);
        Assert.Contains(WellKnownPermissionKeys.DefinitionsRead, updated.PermissionKeys);
    }

    /// <summary>管理者は API キーを発行し一覧できる。</summary>
    [Fact]
    public async Task CreateApiKeyAsync_Admin_ReturnsPlainKeyOnce()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory, new DefaultIdGenerator());
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin-apikeys@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var created = await service.CreateApiKeyAsync(
            adminId,
            new CreateAdminApiKeyRequest
            {
                Name = "CI Runner",
                AllowedScopes = [WellKnownPermissionKeys.ExecutionsRead]
            },
            CancellationToken.None);
        var list = await service.ListApiKeysAsync(adminId, CancellationToken.None);

        // Assert
        Assert.StartsWith("stv_", created.PlainKey, StringComparison.Ordinal);
        Assert.Equal("CI Runner", created.Name);
        Assert.Single(created.AllowedScopes);
        Assert.Contains(created.ApiKeyId, list.Select(item => item.ApiKeyId));
        Assert.DoesNotContain(list, item => item.AllowedScopes.Contains(WellKnownPermissionKeys.TenantAdmin));
    }

    /// <summary>API キーに modules.reload を allowed_scopes で指定できる。</summary>
    [Fact]
    public async Task CreateApiKeyAsync_ModulesReloadScope_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory, new DefaultIdGenerator());
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin-modules-scope@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var created = await service.CreateApiKeyAsync(
            adminId,
            new CreateAdminApiKeyRequest
            {
                Name = "Module Reloader",
                AllowedScopes = [WellKnownPermissionKeys.ModulesReload]
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(WellKnownPermissionKeys.ModulesReload, Assert.Single(created.AllowedScopes));
    }

    /// <summary>管理者は API キーを失効できる。</summary>
    [Fact]
    public async Task RevokeApiKeyAsync_Admin_DeactivatesPrincipal()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var platform = new PlatformDataAccess(database.Factory, new DefaultIdGenerator());
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin-revoke@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        var created = await service.CreateApiKeyAsync(
            adminId,
            new CreateAdminApiKeyRequest
            {
                Name = "Revoke Me",
                AllowedScopes = [WellKnownPermissionKeys.DefinitionsRead]
            },
            CancellationToken.None);

        // Act
        await service.RevokeApiKeyAsync(adminId, created.ApiKeyId, CancellationToken.None);
        var list = await service.ListApiKeysAsync(adminId, CancellationToken.None);

        // Assert
        var revoked = Assert.Single(list, item => item.ApiKeyId == created.ApiKeyId);
        Assert.False(revoked.IsActive);
    }

    /// <summary>メールなしでユーザーを作成できる。</summary>
    [Fact]
    public async Task CreateUserAsync_WithoutEmail_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "admin@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var created = await service.CreateUserAsync(
            adminId,
            new CreateAdminUserRequest
            {
                Username = "no-mail",
                Password = "initial-password"
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("no-mail", created.Username);
        Assert.Null(created.Email);
    }

    /// <summary>テナントが違えば同一 username を作成できる。</summary>
    [Fact]
    public async Task CreateUserAsync_SameUsernameDifferentTenant_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var defaultAdminId = await SecurityTestSeed.SeedUserAsync(
            database, "admin@example.com", "password", isTenantAdmin: true);
        var t1AdminId = await SecurityTestSeed.SeedUserAsync(
            database, "t1-admin", "password", isTenantAdmin: true, tenantId: TestTenantIds.T1TenantId);
        var defaultContext = new SettableTenantContextAccessor();
        var defaultService = CreateService(database, defaultContext, defaultAdminId);
        var t1Context = new SettableTenantContextAccessor();
        var t1Service = CreateService(database, t1Context, t1AdminId, TestTenantIds.T1Context);

        // Act
        var defaultUser = await defaultService.CreateUserAsync(
            defaultAdminId,
            new CreateAdminUserRequest { Username = "shared", Password = "initial-password" },
            CancellationToken.None);
        var t1User = await t1Service.CreateUserAsync(
            t1AdminId,
            new CreateAdminUserRequest { Username = "shared", Password = "initial-password" },
            CancellationToken.None);

        // Assert
        Assert.Equal("shared", defaultUser.Username);
        Assert.Equal("shared", t1User.Username);
        Assert.NotEqual(defaultUser.UserId, t1User.UserId);
    }

    /// <summary>テナントが違えば同一メールを付けられる。</summary>
    [Fact]
    public async Task CreateUserAsync_SameEmailDifferentTenant_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var defaultAdminId = await SecurityTestSeed.SeedUserAsync(
            database, "admin@example.com", "password", isTenantAdmin: true);
        var t1AdminId = await SecurityTestSeed.SeedUserAsync(
            database, "t1-admin", "password", isTenantAdmin: true, tenantId: TestTenantIds.T1TenantId);
        var defaultContext = new SettableTenantContextAccessor();
        var defaultService = CreateService(database, defaultContext, defaultAdminId);
        var t1Context = new SettableTenantContextAccessor();
        var t1Service = CreateService(database, t1Context, t1AdminId, TestTenantIds.T1Context);

        // Act
        var defaultUser = await defaultService.CreateUserAsync(
            defaultAdminId,
            new CreateAdminUserRequest
            {
                Username = "ops-a",
                Email = "ops@example.com",
                Password = "initial-password"
            },
            CancellationToken.None);
        var t1User = await t1Service.CreateUserAsync(
            t1AdminId,
            new CreateAdminUserRequest
            {
                Username = "ops-b",
                Email = "ops@example.com",
                Password = "initial-password"
            },
            CancellationToken.None);

        // Assert
        Assert.Equal("ops@example.com", defaultUser.Email);
        Assert.Equal("ops@example.com", t1User.Email);
    }
}
