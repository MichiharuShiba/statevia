namespace Statevia.Core.Application.Services;

/// <summary>Principal の Identity（Live）検証。</summary>
internal static class PrincipalIdentityAuthorization
{
    internal const string PrincipalInactiveCode = "PRINCIPAL_INACTIVE";

    /// <summary>
    /// Principal が解決・有効であることを検証する。無効・論理削除は fail-closed。
    /// </summary>
    /// <param name="principal">Principal 情報。</param>
    public static void EnsureLive(PrincipalInfo principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!principal.IsActive || principal.DisabledAt is not null || principal.DeletedAt is not null)
            throw new ForbiddenException("Principal is not active.", PrincipalInactiveCode);
    }
}
