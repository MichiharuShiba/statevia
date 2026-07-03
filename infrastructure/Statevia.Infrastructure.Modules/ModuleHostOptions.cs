namespace Statevia.Infrastructure.Modules;

/// <summary>ModuleHost の設定。</summary>
internal sealed class ModuleHostOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules";

    /// <summary>
    /// Module Action の所有者テナント（<c>tenants.tenant_id</c> UUID 文字列）。
    /// 未設定時は ModuleLoadHostedService が既定テナントを解決する。
    /// </summary>
    public string? OwnerTenantId { get; set; }
}
