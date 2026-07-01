namespace Statevia.Service.Api.Application.Security;

/// <summary>テナントライフサイクル遷移の許可・禁止を検証する。</summary>
internal static class LifecycleTransitionPolicy
{
    /// <summary>新規テナントの初期状態。</summary>
    public static TenantLifecycle InitialState => TenantLifecycle.Active;

    /// <summary>
    /// <paramref name="from"/> から <paramref name="to"/> への遷移が許可されるか判定する。
    /// </summary>
    /// <param name="from">現在の状態。</param>
    /// <param name="to">遷移先。</param>
    /// <returns>許可される場合 <see langword="true"/>。</returns>
    public static bool CanTransition(TenantLifecycle from, TenantLifecycle to)
    {
        if (from == to)
            return true;

        return (from, to) switch
        {
            (TenantLifecycle.Active, TenantLifecycle.Suspended) => true,
            (TenantLifecycle.Active, TenantLifecycle.Archived) => true,
            (TenantLifecycle.Suspended, TenantLifecycle.Active) => true,
            (TenantLifecycle.Suspended, TenantLifecycle.Archived) => true,
            _ => false
        };
    }

    /// <summary>
    /// 遷移が禁止の場合 <see cref="ArgumentException"/> を送出する。
    /// </summary>
    /// <param name="from">現在の状態。</param>
    /// <param name="to">遷移先。</param>
    public static void EnsureCanTransition(TenantLifecycle from, TenantLifecycle to)
    {
        if (!CanTransition(from, to))
            throw new ArgumentException($"Lifecycle transition from {from} to {to} is not allowed.");
    }
}
