using Microsoft.Extensions.Hosting;

namespace Statevia.Infrastructure.Security.Configuration;

/// <summary>JWT 認証設定。</summary>
/// <remarks>
/// <para>
/// Development では既定 SigningKey を許可する。
/// Production / Staging では既定値と <see cref="MinSigningKeyLength"/> 文字未満を起動時に拒否する。
/// </para>
/// </remarks>
internal sealed class JwtAuthOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Auth:Jwt";

    /// <summary>
    /// Development 既定の署名鍵。リポジトリ既知のため Production / Staging では使えない。
    /// </summary>
    internal const string DevelopmentSigningKey = "dev-only-change-me-statevia-jwt-signing-key-32chars-min";

    /// <summary>
    /// Production / Staging で要求する SigningKey の最小文字数。HMAC-SHA256 の 32 バイト鍵に合わせる。
    /// </summary>
    internal const int MinSigningKeyLength = 32;

    /// <summary>制限環境での SigningKey 検証失敗メッセージ。鍵値は含めない。</summary>
    internal const string RestrictedEnvironmentSigningKeyMessage =
        "Auth:Jwt:SigningKey must not use the development default and must be at least 32 characters in Production and Staging.";

    /// <summary>署名用シークレット（本番では環境変数必須）。</summary>
    public string SigningKey { get; set; } = DevelopmentSigningKey;

    /// <summary>発行者。</summary>
    public string Issuer { get; set; } = "statevia-service-api";

    /// <summary>対象。</summary>
    public string Audience { get; set; } = "statevia-clients";

    /// <summary>トークン有効期間（分）。</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>既定鍵または短すぎる鍵を拒否する環境か。</summary>
    /// <param name="environment">ホスト環境。</param>
    /// <returns>Production または Staging なら <see langword="true"/>。</returns>
    internal static bool IsRestrictedHostEnvironment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsProduction() || environment.IsStaging();
    }

    /// <summary>制限環境で受理できる SigningKey か。失敗メッセージに鍵値を含めないこと。</summary>
    /// <param name="signingKey">設定された署名鍵。</param>
    /// <returns>既定値でなく、かつ最小長以上なら <see langword="true"/>。</returns>
    internal static bool MeetsRestrictedEnvironmentSigningKeyPolicy(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            return false;
        if (signingKey.Length < MinSigningKeyLength)
            return false;
        return !string.Equals(signingKey, DevelopmentSigningKey, StringComparison.Ordinal);
    }
}
