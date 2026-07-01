namespace Statevia.Service.Api.Application.Security;

/// <summary>権限カタログ 1 件（semantic key と表示メタの分離）。</summary>
/// <param name="PermissionKey">semantic key。</param>
/// <param name="DisplayLabel">UI 向けラベル。</param>
/// <param name="DisplayKey">i18n 辞書キー（任意）。</param>
internal sealed record PermissionCatalogEntry(
    string PermissionKey,
    string DisplayLabel,
    string? DisplayKey);

/// <summary>初版のシステム権限カタログ。</summary>
internal static class PermissionCatalog
{
    /// <summary>登録対象の権限定義。</summary>
    public static IReadOnlyList<PermissionCatalogEntry> Entries { get; } =
    [
        new(WellKnownPermissionKeys.DefinitionsRead, "Read definitions", "permissions.definitionsRead"),
        new(WellKnownPermissionKeys.DefinitionsWrite, "Write definitions", "permissions.definitionsWrite"),
        new(WellKnownPermissionKeys.ExecutionsRead, "Read executions", "permissions.executionsRead"),
        new(WellKnownPermissionKeys.ExecutionsWrite, "Write executions", "permissions.executionsWrite"),
        new(WellKnownPermissionKeys.TenantAdmin, "Tenant administration", "permissions.tenantAdmin")
    ];
}
