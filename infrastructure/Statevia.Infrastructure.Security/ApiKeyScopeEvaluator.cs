namespace Statevia.Infrastructure.Security;

/// <summary>API キー有効スコープを交差のみで評価する。</summary>
internal static class ApiKeyScopeEvaluator
{
    /// <summary>
    /// 展開済み許可集合と <paramref name="allowedScopes"/> の交差を返す。
    /// </summary>
    /// <param name="expandedPermissions">グループ等から展開済みの許可集合。</param>
    /// <param name="allowedScopes">API キーに設定された許可集合。</param>
    /// <returns>有効スコープ集合。</returns>
    public static IReadOnlySet<string> IntersectEffectiveScopes(
        IEnumerable<string> expandedPermissions,
        IEnumerable<string> allowedScopes)
    {
        ArgumentNullException.ThrowIfNull(expandedPermissions);
        ArgumentNullException.ThrowIfNull(allowedScopes);

        var expanded = expandedPermissions
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var allowed = allowedScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .ToHashSet(StringComparer.Ordinal);
        expanded.IntersectWith(allowed);
        return expanded;
    }
}
