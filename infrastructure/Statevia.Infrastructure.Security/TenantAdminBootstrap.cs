using Microsoft.EntityFrameworkCore;

using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Security;

/// <summary>テナント管理者ブートストラップ結果。</summary>
/// <param name="TenantId">テナント内部 ID。</param>
/// <param name="TenantKey">外部テナントキー。</param>
/// <param name="UserId">ユーザー ID。</param>
/// <param name="PrincipalId">Principal ID。</param>
/// <param name="Email">メールアドレス。</param>
/// <param name="Created">新規作成した場合 true。既存スキップ時は false。</param>
internal sealed record TenantAdminBootstrapResult(
    Guid TenantId,
    string TenantKey,
    Guid UserId,
    Guid PrincipalId,
    string Email,
    bool Created);

/// <summary>
/// 初回テナント管理者（Principal + User + user_principals）を作成する。
/// パスワードは <see cref="PasswordCredentialService"/> と同じアルゴリズムでハッシュする。
/// </summary>
internal sealed class TenantAdminBootstrap
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IPlatformDataAccess _platformDataAccess;
    private readonly PasswordCredentialService _passwordCredentialService;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantAdminBootstrap(
        IDbContextFactory<CoreDbContext> dbFactory,
        IPlatformDataAccess platformDataAccess,
        PasswordCredentialService passwordCredentialService)
    {
        _dbFactory = dbFactory;
        _platformDataAccess = platformDataAccess;
        _passwordCredentialService = passwordCredentialService;
    }

    /// <summary>
    /// テナント管理者を作成する。
    /// </summary>
    /// <param name="tenantKey">外部テナントキー。</param>
    /// <param name="email">メールアドレス（テナント内一意）。</param>
    /// <param name="password">平文パスワード（ログに出力しないこと）。</param>
    /// <param name="displayName">Principal 表示名（未指定時は email）。</param>
    /// <param name="skipIfExists">同一テナント・メールが既にいる場合は何もしない。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>作成またはスキップ結果。</returns>
    public async Task<TenantAdminBootstrapResult> CreateTenantAdminAsync(
        string tenantKey,
        string email,
        string password,
        string? displayName,
        bool skipIfExists,
        CancellationToken cancellationToken)
    {
        var normalizedTenantKey = tenantKey.Trim();
        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTenantKey))
            throw new ArgumentException("tenantKey is required.", nameof(tenantKey));
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("password is required.", nameof(password));

        if (string.Equals(normalizedTenantKey, TenantRequestHeaders.DefaultTenantId, StringComparison.Ordinal))
            await _platformDataAccess.EnsureDefaultTenantAsync(cancellationToken).ConfigureAwait(false);

        await _platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);

        var tenant = await _platformDataAccess
            .FindTenantByKeyAsync(normalizedTenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            throw new InvalidOperationException(
                $"Tenant '{normalizedTenantKey}' was not found. Create the tenant first (see docs/operations-tenant-bootstrap.md).");
        }

        var existingLogin = await _platformDataAccess
            .FindLoginCredentialAsync(normalizedTenantKey, normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (existingLogin is not null)
        {
            if (skipIfExists)
            {
                return new TenantAdminBootstrapResult(
                    tenant.TenantId,
                    tenant.TenantKey,
                    existingLogin.User.UserId,
                    existingLogin.Principal.PrincipalId,
                    normalizedEmail,
                    Created: false);
            }

            throw new InvalidOperationException(
                $"User '{normalizedEmail}' already exists for tenant '{normalizedTenantKey}'.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var emailTaken = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenant.TenantId && u.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (emailTaken)
        {
            if (skipIfExists)
            {
                throw new InvalidOperationException(
                    $"User row for '{normalizedEmail}' exists but is not login-ready; fix data or use a different email.");
            }

            throw new InvalidOperationException(
                $"User '{normalizedEmail}' already exists for tenant '{normalizedTenantKey}'.");
        }

        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var principalDisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim();

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenant.TenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = principalDisplayName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = tenant.TenantId,
            Email = normalizedEmail,
            PasswordHash = _passwordCredentialService.HashPassword(password),
            IsTenantAdmin = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TenantAdminBootstrapResult(
            tenant.TenantId,
            tenant.TenantKey,
            userId,
            principalId,
            normalizedEmail,
            Created: true);
    }
}
