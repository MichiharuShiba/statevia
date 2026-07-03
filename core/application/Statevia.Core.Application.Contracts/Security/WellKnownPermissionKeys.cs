namespace Statevia.Core.Application.Contracts.Security;

/// <summary>グローバル権限カタログの semantic key（表示メタは <see cref="PermissionCatalog"/>）。</summary>
public static class WellKnownPermissionKeys
{
    /// <summary>定義の読み取り。</summary>
    public const string DefinitionsRead = "definitions.read";

    /// <summary>定義の書き込み。</summary>
    public const string DefinitionsWrite = "definitions.write";

    /// <summary>実行の読み取り。</summary>
    public const string ExecutionsRead = "executions.read";

    /// <summary>実行の書き込み（開始・キャンセル等）。</summary>
    public const string ExecutionsWrite = "executions.write";

    /// <summary>テナント管理（ユーザー・グループ・API キー発行）。</summary>
    public const string TenantAdmin = "tenant.admin";
}
