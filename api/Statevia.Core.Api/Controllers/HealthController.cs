using Microsoft.AspNetCore.Mvc;

namespace Statevia.Core.Api.Controllers;

/// <summary>
/// 死活監視（<c>/v1/health</c>）。
/// </summary>
[ApiController]
[Route("v1")]
public sealed class HealthController : ControllerBase
{
    /// <summary>GET /v1/health — サービス死活。</summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new HealthResponse { Status = "ok" });
}

/// <summary>GET /v1/health の応答本文。</summary>
public sealed class HealthResponse
{
    /// <summary>死活状態（例: ok）。</summary>
    public string Status { get; init; } = "";
}
