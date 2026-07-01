namespace Statevia.Core.Application.Contracts.Security;

/// <summary><see cref="ITenantContext"/> の共通ヘルパー。</summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// 解決済み <c>tenants.tenant_id</c> を返す。未解決時は <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="context">テナント文脈。</param>
    /// <returns><c>tenants.tenant_id</c>。</returns>
    public static Guid GetRequiredTenantId(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TenantId
            ?? throw new InvalidOperationException("Tenant context is not resolved.");
    }

    /// <summary>
    /// 解決済み <c>tenants.tenant_key</c> を返す。未解決時は <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="context">テナント文脈。</param>
    /// <returns><c>tenants.tenant_key</c>（HTTP <c>X-Tenant-Id</c> の値）。</returns>
    public static string GetRequiredTenantKey(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TenantKey
            ?? throw new InvalidOperationException("Tenant context is not resolved.");
    }
}
