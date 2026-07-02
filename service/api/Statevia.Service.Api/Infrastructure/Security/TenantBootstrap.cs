using Microsoft.EntityFrameworkCore;

using Statevia.Service.Api.Hosting;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Infrastructure.Security;

/// <summary>テナント作成結果。</summary>
/// <param name="TenantId">テナント内部 ID。</param>
/// <param name="TenantKey">外部テナントキー。</param>
/// <param name="DisplayName">表示名。</param>
/// <param name="Created">新規作成した場合 true。</param>
internal sealed record TenantBootstrapResult(
    Guid TenantId,
    string TenantKey,
    string DisplayName,
    bool Created);

/// <summary>追加テナント（<c>tenants</c> 行）を作成する。</summary>
internal sealed class TenantBootstrap
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantBootstrap(
        IDbContextFactory<CoreDbContext> dbFactory,
        IPlatformDataAccess platformDataAccess)
    {
        _dbFactory = dbFactory;
        _platformDataAccess = platformDataAccess;
    }

    /// <summary>
    /// テナントを作成する。<paramref name="tenantKey"/> は作成後変更しない（immutable）。
    /// </summary>
    /// <param name="tenantKey">外部テナントキー。</param>
    /// <param name="displayName">表示名（未指定時は tenantKey）。</param>
    /// <param name="skipIfExists">既存キーがあれば何もしない。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    public async Task<TenantBootstrapResult> CreateTenantAsync(
        string tenantKey,
        string? displayName,
        bool skipIfExists,
        CancellationToken cancellationToken)
    {
        var normalizedKey = TenantKeyValidator.Normalize(tenantKey);
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? normalizedKey
            : displayName.Trim();

        if (normalizedDisplayName.Length > 256)
            throw new ArgumentException("displayName must be at most 256 characters.", nameof(displayName));

        var existing = await _platformDataAccess
            .FindTenantByKeyAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (skipIfExists)
            {
                return new TenantBootstrapResult(
                    existing.TenantId,
                    existing.TenantKey,
                    existing.DisplayName,
                    Created: false);
            }

            throw new InvalidOperationException($"Tenant '{normalizedKey}' already exists.");
        }

        if (string.Equals(normalizedKey, TenantHeader.DefaultTenantId, StringComparison.Ordinal))
        {
            await _platformDataAccess.EnsureDefaultTenantAsync(cancellationToken).ConfigureAwait(false);
            var defaultTenant = await _platformDataAccess
                .FindTenantByKeyAsync(normalizedKey, cancellationToken)
                .ConfigureAwait(false);
            if (defaultTenant is null)
                throw new InvalidOperationException("Default tenant seed failed.");

            await _platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);
            return new TenantBootstrapResult(
                defaultTenant.TenantId,
                defaultTenant.TenantKey,
                defaultTenant.DisplayName,
                Created: true);
        }

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Tenants.Add(new TenantRow
        {
            TenantId = tenantId,
            TenantKey = normalizedKey,
            DisplayName = normalizedDisplayName,
            Lifecycle = LifecycleTransitionPolicy.InitialState,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _platformDataAccess.EnsurePermissionCatalogAsync(cancellationToken).ConfigureAwait(false);

        return new TenantBootstrapResult(
            tenantId,
            normalizedKey,
            normalizedDisplayName,
            Created: true);
    }
}
