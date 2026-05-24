namespace Statevia.Core.Api.Contracts.Auth;

/// <summary>ログイン要求。</summary>
public sealed class LoginRequest
{
    /// <summary>外部向けテナントキー。</summary>
    public string TenantKey { get; set; } = "";

    /// <summary>メールアドレス。</summary>
    public string Email { get; set; } = "";

    /// <summary>平文パスワード。</summary>
    public string Password { get; set; } = "";
}

/// <summary>ログイン成功応答。</summary>
public sealed class LoginResponse
{
    /// <summary>JWT アクセストークン。</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>有効期限（UTC）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>テナント内部 ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>外部キー。</summary>
    public string TenantKey { get; set; } = "";

    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }
}

/// <summary>認証済み Principal 情報。</summary>
public sealed class AuthMeResponse
{
    /// <summary>テナント内部 ID。</summary>
    public Guid TenantId { get; set; }

    /// <summary>外部キー。</summary>
    public string TenantKey { get; set; } = "";

    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>メールアドレス。</summary>
    public string Email { get; set; } = "";

    /// <summary>テナント管理者か。</summary>
    public bool IsTenantAdmin { get; set; }
}
