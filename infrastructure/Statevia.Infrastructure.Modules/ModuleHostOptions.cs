namespace Statevia.Infrastructure.Modules;

/// <summary>ModuleHost の設定。</summary>
/// <remarks>
/// セクション <c>Statevia:Modules</c>。
/// テナント別レイアウトは <c>{Path}/{tenantKey}/&lt;module&gt;/</c> を正本とする。
/// </remarks>
internal sealed class ModuleHostOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules";

    /// <summary>
    /// Module Action の所有者テナント（<c>tenants.tenant_id</c> UUID 文字列）。
    /// 設定時は全 Module に固定適用する（レガシー）。未設定時はテナント連動解決。
    /// </summary>
    public string? OwnerTenantId { get; set; }
}
