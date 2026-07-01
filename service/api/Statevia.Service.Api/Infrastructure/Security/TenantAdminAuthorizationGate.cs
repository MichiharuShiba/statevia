using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Infrastructure.Security;

/// <summary>テナント管理者向け API の認可ゲート。</summary>
internal static class TenantAdminAuthorizationGate
{
    /// <summary>認証済みテナント管理者であることを検証する。</summary>
    /// <param name="tenantContext">テナントコンテキスト。</param>
    /// <param name="tenantAdminAuthorization">管理者判定。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    public static async Task EnsureTenantAdminAsync(
        ITenantContextAccessor tenantContext,
        ITenantAdminAuthorization tenantAdminAuthorization,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved || tenantContext.PrincipalId is not { } principalId)
        {
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");
        }

        if (!await tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("Tenant administrator required.", "FORBIDDEN");
        }
    }
}
