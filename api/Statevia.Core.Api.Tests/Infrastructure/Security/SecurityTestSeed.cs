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
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = database.Factory.CreateDbContext();
        foreach (var entry in PermissionCatalog.Entries)
        {
            if (await db.PermissionDefinitions.IgnoreQueryFilters().AnyAsync(p => p.PermissionKey == entry.PermissionKey))
                continue;

            db.PermissionDefinitions.Add(new PermissionDefinitionRow
            {
                PermissionDefinitionId = Guid.NewGuid(),
                PermissionKey = entry.PermissionKey,
                DisplayLabel = entry.DisplayLabel,
                DisplayKey = entry.DisplayKey,
                IsSystem = true,
                CreatedAt = now
            });
        }

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = TestTenantIds.DefaultInternalId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = "ci-runner",
            IsActive = principalIsActive,
            DeletedAt = principalDeletedAt,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = TestTenantIds.DefaultInternalId,
            Email = $"ci-{principalId:N}@example.com",
            PasswordHash = "unused",
            IsTenantAdmin = false,
            IsActive = principalIsActive,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
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
        db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });
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
}
