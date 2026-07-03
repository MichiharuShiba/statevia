namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Principal のスコープ。</summary>
public enum PrincipalScope
{
    /// <summary>テナント内の実行主体。</summary>
    Tenant = 0,

    /// <summary>プラットフォーム横断（capability 集合で権限化）。</summary>
    Platform = 1
}

/// <summary>Principal の種別。</summary>
public enum PrincipalType
{
    /// <summary>人間ユーザー。</summary>
    User = 0,

    /// <summary>サービスアカウント。</summary>
    ServiceAccount = 1,

    /// <summary>システム予約主体。</summary>
    System = 2
}
