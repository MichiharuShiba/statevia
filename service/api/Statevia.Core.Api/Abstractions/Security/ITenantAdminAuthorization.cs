namespace Statevia.Core.Api.Abstractions.Security;

/// <summary>テナント管理者判定（<c>users.is_tenant_admin</c>。グループのみに依存しない）。</summary>
public interface ITenantAdminAuthorization
{
    /// <summary>Principal がテナント管理者か。</summary>
    Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken);
}
