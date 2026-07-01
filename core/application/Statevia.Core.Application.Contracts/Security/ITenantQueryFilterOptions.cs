namespace Statevia.Core.Application.Contracts.Security;

/// <summary>EF HasQueryFilter の有効／無効（テストでは無効化可能）。</summary>
public interface ITenantQueryFilterOptions
{
    /// <summary>テナント QueryFilter を適用するか。</summary>
    bool IsEnabled { get; }
}

/// <summary>本番既定: QueryFilter 有効。</summary>
public sealed class EnabledTenantQueryFilterOptions : ITenantQueryFilterOptions
{
    /// <summary>共有インスタンス。</summary>
    public static readonly EnabledTenantQueryFilterOptions Instance = new();

    private EnabledTenantQueryFilterOptions() { }

    /// <inheritdoc />
    public bool IsEnabled => true;
}

/// <summary>テスト用: QueryFilter 無効。</summary>
public sealed class DisabledTenantQueryFilterOptions : ITenantQueryFilterOptions
{
    /// <summary>共有インスタンス。</summary>
    public static readonly DisabledTenantQueryFilterOptions Instance = new();

    private DisabledTenantQueryFilterOptions() { }

    /// <inheritdoc />
    public bool IsEnabled => false;
}
