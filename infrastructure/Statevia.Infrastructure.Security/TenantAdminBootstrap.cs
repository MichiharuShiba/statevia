using Microsoft.EntityFrameworkCore;

using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Infrastructure.Security;

/// <summary>テナント管理者ブートストラップ結果。</summary>
/// <param name="TenantId">テナント内部 ID。</param>
/// <param name="TenantKey">外部テナントキー。</param>
/// <param name="UserId">ユーザー ID。</param>
/// <param name="PrincipalId">Principal ID。</param>
/// <param name="Username">ログインユーザー名。</param>
/// <param name="Email">任意の連絡先メール。</param>
/// <param name="Created">新規作成した場合 true。既存スキップ時は false。</param>
internal sealed record TenantAdminBootstrapResult(
    Guid TenantId,
    string TenantKey,
    Guid UserId,
    Guid PrincipalId,
    string Username,
    string? Email,
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
    private readonly IIdGenerator _idGenerator;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="dbFactory">DbContext 工場。</param>
    /// <param name="platformDataAccess">Platform データアクセス。</param>
    /// <param name="passwordCredentialService">パスワードハッシュ。</param>
    /// <param name="idGenerator">永続 UUID 採番。</param>
    public TenantAdminBootstrap(
        IDbContextFactory<CoreDbContext> dbFactory,
        IPlatformDataAccess platformDataAccess,
        PasswordCredentialService passwordCredentialService,
        IIdGenerator idGenerator)
    {
        _dbFactory = dbFactory;
        _platformDataAccess = platformDataAccess;
        _passwordCredentialService = passwordCredentialService;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// テナント管理者を作成する。
    /// </summary>
    /// <param name="tenantKey">外部テナントキー。</param>
    /// <param name="username">ログインユーザー名（テナント内一意。先頭・末尾は英数字。途中のみハイフン・アンダースコア・ドット。最大 64 文字）。</param>
    /// <param name="password">平文パスワード（8〜128 文字。空白なし。記号可。ログに出力しないこと）。</param>
    /// <param name="displayName">Principal 表示名（未指定時は username）。</param>
    /// <param name="skipIfExists">同一テナント・ユーザー名が既にいる場合は何もしない。</param>
    /// <param name="email">任意の連絡先メール（テナント内一意）。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>作成またはスキップ結果。</returns>
    public async Task<TenantAdminBootstrapResult> CreateTenantAdminAsync(
        string tenantKey,
        string username,
        string password,
        string? displayName,
        bool skipIfExists,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeAndValidate(tenantKey, username, password, email);

        if (string.Equals(identity.TenantKey, TenantRequestHeaders.DefaultTenantId, StringComparison.Ordinal))
            await _platformDataAccess.EnsureDefaultTenantAsync(cancellationToken).ConfigureAwait(false);

        await _platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);

        var tenant = await _platformDataAccess
            .FindTenantByKeyAsync(identity.TenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            throw new InvalidOperationException(
                $"Tenant '{identity.TenantKey}' was not found. Create the tenant first (see docs/operations-tenant-bootstrap.md).");
        }

        var existingResult = await TrySkipExistingLoginAsync(
                tenant,
                identity,
                skipIfExists,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingResult is not null)
            return existingResult;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await EnsureUsernameAvailableAsync(db, tenant, identity.Username, skipIfExists, cancellationToken)
            .ConfigureAwait(false);
        await EnsureEmailAvailableAsync(db, tenant, identity.Email, cancellationToken).ConfigureAwait(false);

        return await InsertTenantAdminAsync(db, tenant, identity, displayName, password, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>入力を正規化し、形式・必須を検証する。</summary>
    private static NormalizedAdminIdentity NormalizeAndValidate(
        string tenantKey,
        string username,
        string password,
        string? email)
    {
        var normalizedTenantKey = tenantKey.Trim();
        var normalizedUsername = username.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTenantKey))
            throw new ArgumentException("tenantKey is required.", nameof(tenantKey));
        if (string.IsNullOrWhiteSpace(normalizedUsername))
            throw new ArgumentException("username is required.", nameof(username));
        if (!UsernameConstraints.IsValid(normalizedUsername))
            throw new ArgumentException(UsernameConstraints.FormatErrorMessage, nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("password is required.", nameof(password));
        if (!PasswordConstraints.IsValid(password))
            throw new ArgumentException(PasswordConstraints.FormatErrorMessage, nameof(password));
        if (normalizedEmail is { Length: > UserEmailConstraints.MaxLength })
            throw new ArgumentException($"email must be at most {UserEmailConstraints.MaxLength} characters.", nameof(email));

        return new NormalizedAdminIdentity(normalizedTenantKey, normalizedUsername, normalizedEmail);
    }

    /// <summary>
    /// 同一ユーザー名のログイン資格がある場合、skip なら結果を返し、そうでなければ例外にする。
    /// 未存在なら null。
    /// </summary>
    private async Task<TenantAdminBootstrapResult?> TrySkipExistingLoginAsync(
        TenantRow tenant,
        NormalizedAdminIdentity identity,
        bool skipIfExists,
        CancellationToken cancellationToken)
    {
        var existingLogin = await _platformDataAccess
            .FindLoginCredentialAsync(identity.TenantKey, identity.Username, cancellationToken)
            .ConfigureAwait(false);
        if (existingLogin is null)
            return null;

        if (skipIfExists)
        {
            return new TenantAdminBootstrapResult(
                tenant.TenantId,
                tenant.TenantKey,
                existingLogin.User.UserId,
                existingLogin.Principal.PrincipalId,
                existingLogin.User.Username,
                existingLogin.User.Email,
                Created: false);
        }

        throw new InvalidOperationException(
            $"User '{identity.Username}' already exists for tenant '{identity.TenantKey}'.");
    }

    /// <summary>users 行に同一ユーザー名がある場合の衝突を処理する。</summary>
    private static async Task EnsureUsernameAvailableAsync(
        CoreDbContext db,
        TenantRow tenant,
        string username,
        bool skipIfExists,
        CancellationToken cancellationToken)
    {
        var usernameTaken = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenant.TenantId && u.Username == username, cancellationToken)
            .ConfigureAwait(false);
        if (!usernameTaken)
            return;

        if (skipIfExists)
        {
            throw new InvalidOperationException(
                $"User row for '{username}' exists but is not login-ready; fix data or use a different username.");
        }

        throw new InvalidOperationException(
            $"User '{username}' already exists for tenant '{tenant.TenantKey}'.");
    }

    /// <summary>連絡先メールがテナント内で未使用であることを確認する。</summary>
    private static async Task EnsureEmailAvailableAsync(
        CoreDbContext db,
        TenantRow tenant,
        string? email,
        CancellationToken cancellationToken)
    {
        if (email is null)
            return;

        var emailTaken = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenant.TenantId && u.Email == email, cancellationToken)
            .ConfigureAwait(false);
        if (!emailTaken)
            return;

        throw new InvalidOperationException(
            $"Email already exists for tenant '{tenant.TenantKey}'.");
    }

    /// <summary>Principal・User・紐付けを挿入する。</summary>
    private async Task<TenantAdminBootstrapResult> InsertTenantAdminAsync(
        CoreDbContext db,
        TenantRow tenant,
        NormalizedAdminIdentity identity,
        string? displayName,
        string password,
        CancellationToken cancellationToken)
    {
        var userId = _idGenerator.NewSequentialGuid();
        var principalId = _idGenerator.NewSequentialGuid();
        var now = DateTime.UtcNow;
        var principalDisplayName = string.IsNullOrWhiteSpace(displayName) ? identity.Username : displayName.Trim();

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
            Username = identity.Username,
            Email = identity.Email,
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
            identity.Username,
            identity.Email,
            Created: true);
    }

    /// <summary>正規化済みのテナントキー・ユーザー名・連絡先メール。</summary>
    private readonly record struct NormalizedAdminIdentity(string TenantKey, string Username, string? Email);
}
