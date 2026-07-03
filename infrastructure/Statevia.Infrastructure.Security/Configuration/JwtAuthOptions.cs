namespace Statevia.Infrastructure.Security.Configuration;

/// <summary>JWT 認証設定。</summary>
internal sealed class JwtAuthOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Auth:Jwt";

    /// <summary>署名用シークレット（本番では環境変数必須）。</summary>
    public string SigningKey { get; set; } = "dev-only-change-me-statevia-jwt-signing-key-32chars-min";

    /// <summary>発行者。</summary>
    public string Issuer { get; set; } = "statevia-core-api";

    /// <summary>対象。</summary>
    public string Audience { get; set; } = "statevia-clients";

    /// <summary>トークン有効期間（分）。</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
