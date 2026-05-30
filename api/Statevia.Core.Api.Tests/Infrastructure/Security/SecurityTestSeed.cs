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
    /// <returns>作成した Principal ID。</returns>
    public static async Task<Guid> SeedUserAsync(
        SqliteTestDatabase database,
        string email,
        string password,
        bool isActive = true)
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
            IsTenantAdmin = true,
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
        var principalId = Guid.NewGuid();
        var apiKeyId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = database.Factory.CreateDbContext();
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
