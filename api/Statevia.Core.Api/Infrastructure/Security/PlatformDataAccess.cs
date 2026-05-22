using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Infrastructure.Security;

/// <summary>ログイン資格情報の lookup 結果。</summary>
/// <param name="Tenant">テナント行。</param>
/// <param name="User">ユーザー行。</param>
/// <param name="Principal">Principal 行。</param>
internal sealed record LoginCredentialLookup(TenantRow Tenant, UserRow User, PrincipalRow Principal);

/// <summary>Platform 専用データアクセス。</summary>
internal interface IPlatformDataAccess
{
    /// <summary><paramref name="tenantKey"/> でテナントを検索する（フィルタ無視）。</summary>
    Task<TenantRow?> FindTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken);

    /// <summary>ログイン用の tenant / user / principal を検索する。</summary>
    Task<LoginCredentialLookup?> FindLoginCredentialAsync(
        string tenantKey,
        string email,
        CancellationToken cancellationToken);

    /// <summary>Principal ID とテナント内部 ID でユーザー情報を取得する。</summary>
    Task<LoginCredentialLookup?> FindUserPrincipalAsync(
        Guid tenantInternalId,
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary>既定テナントが無ければ作成する。</summary>
    Task EnsureDefaultTenantAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Platform 専用データアクセス。<c>IgnoreQueryFilters</c> は本クラスにのみ閉じる。
/// </summary>
internal sealed class PlatformDataAccess : IPlatformDataAccess
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public PlatformDataAccess(IDbContextFactory<CoreDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<TenantRow?> FindTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantKey == tenantKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<LoginCredentialLookup?> FindLoginCredentialAsync(
        string tenantKey,
        string email,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantKey == tenantKey, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
            return null;

        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.TenantId && u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive)
            return null;

        var principal = await db.UserPrincipals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(up => up.UserId == user.UserId)
            .Join(
                db.Principals.IgnoreQueryFilters().AsNoTracking(),
                up => up.PrincipalId,
                p => p.PrincipalId,
                (_, p) => p)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (principal is null || !principal.IsActive || principal.DeletedAt is not null)
            return null;

        return new LoginCredentialLookup(tenant, user, principal);
    }

    /// <inheritdoc />
    public async Task<LoginCredentialLookup?> FindUserPrincipalAsync(
        Guid tenantInternalId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantInternalId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
            return null;

        var userPrincipal = await db.UserPrincipals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(up => up.PrincipalId == principalId)
            .Join(
                db.Users.IgnoreQueryFilters().AsNoTracking(),
                up => up.UserId,
                u => u.UserId,
                (up, u) => new { up, u })
            .FirstOrDefaultAsync(x => x.u.TenantId == tenantInternalId, cancellationToken)
            .ConfigureAwait(false);

        if (userPrincipal is null || !userPrincipal.u.IsActive)
            return null;

        var principal = await db.Principals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PrincipalId == principalId, cancellationToken)
            .ConfigureAwait(false);

        if (principal is null || !principal.IsActive || principal.DeletedAt is not null)
            return null;

        return new LoginCredentialLookup(tenant, userPrincipal.u, principal);
    }

    /// <inheritdoc />
    public async Task EnsureDefaultTenantAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantKey == TenantHeader.DefaultTenantId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return;

        var now = DateTime.UtcNow;
        db.Tenants.Add(new TenantRow
        {
            TenantId = DefaultTenantId,
            TenantKey = TenantHeader.DefaultTenantId,
            DisplayName = "Default",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
