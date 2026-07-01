using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Controllers;

/// <summary>認証 API。</summary>
[ApiController]
[Route("v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AuthController(IAuthService authService, ITenantContextAccessor tenantContext)
    {
        _authService = authService;
        _tenantContext = tenantContext;
    }

    /// <summary>POST /v1/auth/login — パスワードログイン。</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    /// <summary>GET /v1/auth/me — 認証済み Principal 情報。</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthMeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthMeResponse>> Me(CancellationToken ct)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId is not { } tenantId ||
            _tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var response = await _authService.GetMeAsync(tenantId, principalId, ct).ConfigureAwait(false);
        return Ok(response);
    }
}
