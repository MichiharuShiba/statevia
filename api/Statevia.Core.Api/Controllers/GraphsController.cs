using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Controllers;

/// <summary>
/// グラフ定義 REST API（<c>/v1/graphs</c>）。
/// </summary>
[ApiController]
[Route("v1/graphs")]
public class GraphsController : ControllerBase
{
    private readonly IGraphDefinitionService _graphService;

    /// <summary>
    /// <see cref="GraphsController"/> を生成する。
    /// </summary>
    /// <param name="graphService">グラフ定義サービス。</param>
    public GraphsController(IGraphDefinitionService graphService)
    {
        _graphService = graphService;
    }

    private const string DefaultTenantId = "default";

    /// <summary>GET /v1/graphs/{graphId} — 契約 4.1 Graph Definition（nodes / edges）を返す。graphId は definition の display_id。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{graphId}")]
    public async Task<ActionResult<GraphDefinitionResponse>> GetByGraphId(string graphId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(graphId))
            return ApiErrorResult.ValidationError("graphId is required");

        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var graph = await _graphService.GetByGraphIdAsync(graphId, tenantId, ct).ConfigureAwait(false);
        return Ok(graph);
    }
}
