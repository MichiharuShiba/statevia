using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Statevia.Infrastructure.Security;

/// <summary>JWT 発行・検証。</summary>
internal sealed class JwtTokenService
{
    public const string TenantIdClaim = "tenant_id";
    public const string TenantKeyClaim = "tenant_key";
    public const string PrincipalIdClaim = "principal_id";

    private readonly JwtAuthOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="options">JWT 設定。</param>
    public JwtTokenService(IOptions<JwtAuthOptions> options)
    {
        _options = options.Value;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    /// <summary>アクセストークンを発行する。</summary>
    /// <param name="tenantId"><c>tenants.tenant_id</c>（JWT <c>tenant_id</c> クレーム）。</param>
    /// <param name="tenantKey">外部キー。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <returns>JWT 文字列と有効期限。</returns>
    public (string Token, DateTime ExpiresAt) IssueAccessToken(Guid tenantId, string tenantKey, Guid principalId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var claims = new[]
        {
            new Claim(TenantIdClaim, tenantId.ToString()),
            new Claim(TenantKeyClaim, tenantKey),
            new Claim(PrincipalIdClaim, principalId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, principalId.ToString())
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>JWT を検証しクレームを返す。</summary>
    /// <param name="token">Bearer トークン。</param>
    /// <returns>検証済みクレーム。失敗時は null。</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, BuildValidationParameters(), out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private TokenValidationParameters BuildValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}
