namespace Statevia.Core.Application.Contracts.Security;

/// <summary>テナント境界 HTTP ヘッダー。</summary>
public static class TenantRequestHeaders
{
    /// <summary>テナント識別子ヘッダー名。</summary>
    public const string HeaderName = "X-Tenant-Id";

    /// <summary>ブートストラップ用の既定テナントキー。</summary>
    public const string DefaultTenantId = "default";
}
