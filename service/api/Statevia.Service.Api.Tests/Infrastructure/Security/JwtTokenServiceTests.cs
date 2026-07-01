using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="JwtTokenService"/> の JWT 発行・検証。</summary>
public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtAuthOptions()));

    /// <summary>発行したトークンは検証に成功する。</summary>
    [Fact]
    public void IssueAccessToken_ValidToken_PassesValidation()
    {
        // Arrange
        var service = CreateService();
        var principalId = Guid.NewGuid();

        // Act
        var (token, expiresAt) = service.IssueAccessToken(
            TestTenantIds.DefaultTenantId,
            "default",
            principalId);
        var principal = service.ValidateToken(token);

        // Assert
        Assert.True(expiresAt > DateTime.UtcNow);
        Assert.NotNull(principal);
        Assert.Equal(principalId.ToString(), principal.FindFirstValue(JwtTokenService.PrincipalIdClaim));
        Assert.Equal("default", principal.FindFirstValue(JwtTokenService.TenantKeyClaim));
        Assert.Equal(TestTenantIds.DefaultTenantId.ToString(), principal.FindFirstValue(JwtTokenService.TenantIdClaim));
    }

    /// <summary>改ざんトークンは検証に失敗する。</summary>
    [Fact]
    public void ValidateToken_TamperedToken_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var (token, _) = service.IssueAccessToken(TestTenantIds.DefaultTenantId, "default", Guid.NewGuid());
        var tampered = token[..^4] + "xxxx";

        // Act
        var principal = service.ValidateToken(tampered);

        // Assert
        Assert.Null(principal);
    }

    /// <summary>期限切れトークンは検証に失敗する。</summary>
    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var options = new JwtAuthOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var service = CreateService();

        // Act
        var principal = service.ValidateToken(tokenString);

        // Assert
        Assert.Null(principal);
    }
}
