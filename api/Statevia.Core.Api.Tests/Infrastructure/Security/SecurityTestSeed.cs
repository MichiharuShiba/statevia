using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary>セキュリティ関連テストの DB シード。</summary>
internal static class SecurityTestSeed
{
    /// <summary>テスト用ユーザーを投入し Principal ID を返す。</summary>
    /// <param name="database">テスト DB。</param>
    /// <param name="email">メールアドレス。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="isActive">ユーザーと Principal をアクティブにするか。</param>
    /// <param name="isTenantAdmin">テナント管理者フラグ。</param>
    /// <returns>作成した Principal ID。</returns>
    public static async Task<Guid> SeedUserAsync(
        SqliteTestDatabase database,
        string email,
        string password,
        bool isActive = true,
        bool isTenantAdmin = true)
    {
        var passwordHasher = new PasswordCredentialService();
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = database.Factory.CreateDbContext();
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = TestTenantIds.DefaultInternalId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = email,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = TestTenantIds.DefaultInternalId,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            IsTenantAdmin = isTenantAdmin,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        await db.SaveChangesAsync();

        return principalId;
    }

    /// <summary>グループ権限付きテストユーザーを投入し Principal ID を返す。</summary>
    /// <param name="database">テスト DB。</param>
    /// <param name="email">メールアドレス。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="permissionKeys">付与する semantic permission key。</param>
    /// <returns>作成した Principal ID。</returns>
    public static async Task<Guid> SeedUserWithGroupPermissionsAsync(
        SqliteTestDatabase database,
        string email,
        string password,
        IReadOnlyList<string> permissionKeys)
    {
        var principalId = await SeedUserAsync(
            database,
            email,
            password,
            isActive: true,
            isTenantAdmin: false).ConfigureAwait(false);

        await using var db = database.Factory.CreateDbContext();
        await EnsurePermissionCatalogAsync(database).ConfigureAwait(false);

        var userId = await db.UserPrincipals
            .Where(up => up.PrincipalId == principalId)
            .Select(up => up.UserId)
            .FirstAsync()
            .ConfigureAwait(false);

        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = TestTenantIds.DefaultInternalId,
            Name = $"test-{email}",
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        foreach (var permissionKey in permissionKeys.Distinct(StringComparer.Ordinal))
        {
            db.GroupPermissions.Add(new GroupPermissionRow
            {
                GroupId = groupId,
                PermissionKey = permissionKey
            });
        }

        db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });
        await db.SaveChangesAsync().ConfigureAwait(false);

        return principalId;
    }

    /// <summary>テスト用 API キーを投入する。</summary>
    /// <param name="database">テスト DB。</param>
    /// <param name="plainKey">平文 API キー。</param>
    /// <param name="allowedScopesJson">許可スコープ JSON。</param>
    /// <param name="expiresAt">有効期限（任意）。</param>
    /// <param name="principalIsActive">Principal をアクティブにするか。</param>
    /// <param name="principalDeletedAt">Principal の論理削除日時（任意）。</param>
    /// <returns>Principal ID、API キー ID、平文キー。</returns>
    public static async Task<(Guid PrincipalId, Guid ApiKeyId, string PlainKey)> SeedApiKeyAsync(
        SqliteTestDatabase database,
        string plainKey = "statevia-test-key",
        string allowedScopesJson = "[\"executions.read\"]",
        DateTime? expiresAt = null,
        bool principalIsActive = true,
        DateTime? principalDeletedAt = null)
    {
        var serviceAccountId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = database.Factory.CreateDbContext();
        await EnsurePermissionCatalogAsync(database).ConfigureAwait(false);

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = TestTenantIds.DefaultInternalId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.ServiceAccount,
            DisplayName = "ci-runner",
            IsActive = principalIsActive,
            DeletedAt = principalDeletedAt,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.ServiceAccounts.Add(new ServiceAccountRow
        {
            ServiceAccountId = serviceAccountId,
            TenantId = TestTenantIds.DefaultInternalId,
            PrincipalId = principalId,
            Name = "ci-runner",
            CreatedAt = now
        });
        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = TestTenantIds.DefaultInternalId,
            Name = "ci-runners",
            IsSystem = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.GroupPermissions.Add(new GroupPermissionRow
        {
            GroupId = groupId,
            PermissionKey = WellKnownPermissionKeys.ExecutionsRead
        });
        db.ServiceAccountGroupMembers.Add(new ServiceAccountGroupMemberRow
        {
            ServiceAccountId = serviceAccountId,
            GroupId = groupId
        });
        db.ApiKeys.Add(new ApiKeyRow
        {
            ApiKeyId = apiKeyId,
            TenantId = TestTenantIds.DefaultInternalId,
            PrincipalId = principalId,
            KeyPrefix = PasswordCredentialService.ApiKeyPrefix(plainKey),
            KeyHash = PasswordCredentialService.HashApiKey(plainKey),
            AllowedScopesJson = allowedScopesJson,
            ExpiresAt = expiresAt,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        return (principalId, apiKeyId, plainKey);
    }

    private static Task EnsurePermissionCatalogAsync(
        SqliteTestDatabase database,
        CancellationToken cancellationToken = default) =>
        new PlatformDataAccess(database.Factory).EnsurePermissionCatalogAsync(cancellationToken);
}
