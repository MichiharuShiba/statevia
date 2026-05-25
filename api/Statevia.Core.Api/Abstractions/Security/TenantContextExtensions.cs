namespace Statevia.Core.Api.Abstractions.Security;

/// <summary><see cref="ITenantContext"/> の共通ヘルパー。</summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// 解決済みテナント内部 UUID を返す。未解決時は <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <param name="context">テナント文脈。</param>
    /// <returns>テナント内部 UUID。</returns>
    public static Guid GetRequiredTenantInternalId(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TenantInternalId
            ?? throw new InvalidOperationException("Tenant context is not resolved.");
    }
}
