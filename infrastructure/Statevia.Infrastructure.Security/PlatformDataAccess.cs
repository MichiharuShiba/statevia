using Microsoft.EntityFrameworkCore;

using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Security;

/// <summary>ログイン資格情報の lookup 結果。</summary>
/// <param name="Tenant">テナント行。</param>
/// <param name="User">ユーザー行。</param>
/// <param name="Principal">Principal 行。</param>
internal sealed record LoginCredentialLookup(TenantRow Tenant, UserRow User, PrincipalRow Principal);
/// <summary>API キー lookup 結果。</summary>
/// <param name="Tenant">テナント行。</param>
/// <param name="ApiKey">API キー行。</param>
/// <param name="Principal">Principal 行。</param>
internal sealed record ApiKeyCredentialLookup(TenantRow Tenant, ApiKeyRow ApiKey, PrincipalRow Principal);

/// <summary>execution_id から解決したテナント境界（投影キュー等のバックグラウンド用）。</summary>
/// <param name="TenantId"><c>tenants.tenant_id</c>。</param>
/// <param name="TenantKey"><c>tenants.tenant_key</c>。</param>
/// <param name="Lifecycle">テナントライフサイクル。</param>
internal sealed record ExecutionTenantLookup(Guid TenantId, string TenantKey, TenantLifecycle Lifecycle);

/// <summary>Platform 専用データアクセス。</summary>
internal interface IPlatformDataAccess
{
    /// <summary><paramref name="tenantKey"/> でテナントを検索する（フィルタ無視）。</summary>
    Task<TenantRow?> FindTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken);

    /// <summary>ライフサイクルが Active のテナントを <c>tenant_key</c> 昇順で返す（フィルタ無視）。</summary>
    Task<IReadOnlyList<TenantRow>> ListActiveTenantsAsync(CancellationToken cancellationToken);

    /// <summary>ログイン用の tenant / user / principal を検索する。</summary>
    Task<LoginCredentialLookup?> FindLoginCredentialAsync(
        string tenantKey,
        string email,
        CancellationToken cancellationToken);

    /// <summary>Principal ID とテナント内部 ID でユーザー情報を取得する。</summary>
    Task<LoginCredentialLookup?> FindUserPrincipalAsync(
        Guid tenantId,
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary>API キー（prefix + hash）から資格情報を検索する。</summary>
    Task<ApiKeyCredentialLookup?> FindApiKeyCredentialAsync(
        string keyPrefix,
        string keyHash,
        CancellationToken cancellationToken);

    /// <summary>API キーの最終利用時刻を更新する。</summary>
    Task TouchApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken);

    /// <summary>既定テナントが無ければ作成する。</summary>
    Task EnsureDefaultTenantAsync(CancellationToken cancellationToken);

    /// <summary>権限カタログが無ければシードする。</summary>
    Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Principal がテナント管理者（<c>is_tenant_admin</c>）か。</summary>
    Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken);

    /// <summary>グループ展開と管理者フラグから Principal の許可 semantic key 集合を返す。</summary>
    Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary>Principal ID で Principal 行を検索する。</summary>
    Task<PrincipalRow?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken);

    /// <summary>Principal の所属グループ（ID と名称）を返す。</summary>
    Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary><paramref name="executionId"/> のテナント境界を検索する（フィルタ無視）。</summary>
    Task<ExecutionTenantLookup?> FindExecutionTenantAsync(Guid executionId, CancellationToken cancellationToken);
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
    public async Task<IReadOnlyList<TenantRow>> ListActiveTenantsAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Lifecycle == TenantLifecycle.Active)
            .OrderBy(t => t.TenantKey)
            .ToListAsync(cancellationToken)
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
        Guid tenantId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken)
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
            .FirstOrDefaultAsync(x => x.u.TenantId == tenantId, cancellationToken)
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
    public async Task<ApiKeyCredentialLookup?> FindApiKeyCredentialAsync(
        string keyPrefix,
        string keyHash,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var apiKey = await db.ApiKeys
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.KeyPrefix == keyPrefix && row.KeyHash == keyHash,
                cancellationToken)
            .ConfigureAwait(false);

        if (apiKey is null)
            return null;

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == apiKey.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
            return null;

        var principal = await db.Principals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PrincipalId == apiKey.PrincipalId, cancellationToken)
            .ConfigureAwait(false);

        if (principal is null)
            return null;

        return new ApiKeyCredentialLookup(tenant, apiKey, principal);
    }

    /// <inheritdoc />
    public async Task TouchApiKeyLastUsedAsync(Guid apiKeyId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var apiKey = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.ApiKeyId == apiKeyId, cancellationToken)
            .ConfigureAwait(false);

        if (apiKey is null)
            return;

        apiKey.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsureDefaultTenantAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantKey == TenantRequestHeaders.DefaultTenantId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return;

        var now = DateTime.UtcNow;
        db.Tenants.Add(new TenantRow
        {
            TenantId = DefaultTenantId,
            TenantKey = TenantRequestHeaders.DefaultTenantId,
            DisplayName = "Default",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existingKeys = await db.PermissionDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(p => p.PermissionKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existing = existingKeys.ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        foreach (var entry in PermissionCatalog.Entries)
        {
            if (existing.Contains(entry.PermissionKey))
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

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var isAdmin = await db.UserPrincipals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(up => up.PrincipalId == principalId)
            .Join(
                db.Users.IgnoreQueryFilters().AsNoTracking(),
                up => up.UserId,
                u => u.UserId,
                (_, u) => u.IsTenantAdmin)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return isAdmin;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (await IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.PermissionDefinitions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var userId = await context.UserPrincipals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(up => up.PrincipalId == principalId)
            .Select(up => (Guid?)up.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userId is not null)
        {
            return await context.UserGroupMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .Join(
                    context.GroupPermissions.IgnoreQueryFilters().AsNoTracking(),
                    m => m.GroupId,
                    gp => gp.GroupId,
                    (_, gp) => gp.PermissionKey)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var serviceAccountId = await context.ServiceAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(sa => sa.PrincipalId == principalId)
            .Select(sa => (Guid?)sa.ServiceAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (serviceAccountId is null)
            return Array.Empty<string>();

        return await context.ServiceAccountGroupMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ServiceAccountId == serviceAccountId)
            .Join(
                context.GroupPermissions.IgnoreQueryFilters().AsNoTracking(),
                m => m.GroupId,
                gp => gp.GroupId,
                (_, gp) => gp.PermissionKey)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PrincipalRow?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Principals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PrincipalId == principalId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var userId = await db.UserPrincipals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(up => up.PrincipalId == principalId)
            .Select(up => (Guid?)up.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userId is not null)
        {
            var rows = await db.UserGroupMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .Join(
                    db.Groups.IgnoreQueryFilters().AsNoTracking(),
                    m => m.GroupId,
                    g => g.GroupId,
                    (_, g) => new { g.GroupId, g.Name })
                .OrderBy(row => row.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.ConvertAll(row => new GroupSnapshot(row.GroupId, row.Name));
        }

        var serviceAccountId = await db.ServiceAccounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(sa => sa.PrincipalId == principalId)
            .Select(sa => (Guid?)sa.ServiceAccountId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (serviceAccountId is null)
            return Array.Empty<GroupSnapshot>();

        var serviceAccountRows = await db.ServiceAccountGroupMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ServiceAccountId == serviceAccountId)
            .Join(
                db.Groups.IgnoreQueryFilters().AsNoTracking(),
                m => m.GroupId,
                g => g.GroupId,
                (_, g) => new { g.GroupId, g.Name })
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return serviceAccountRows.ConvertAll(row => new GroupSnapshot(row.GroupId, row.Name));
    }

    /// <inheritdoc />
    public async Task<ExecutionTenantLookup?> FindExecutionTenantAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = await db.Executions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(execution => execution.ExecutionId == executionId)
            .Select(execution => (Guid?)execution.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenantId is null)
            return null;

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        return tenant is null
            ? null
            : new ExecutionTenantLookup(tenant.TenantId, tenant.TenantKey, tenant.Lifecycle);
    }
}
