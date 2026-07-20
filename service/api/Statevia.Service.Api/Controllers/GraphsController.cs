using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Controllers;

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

    /// <summary>GET /v1/graphs/{graphId} — 契約 4.1 Graph Definition（nodes / edges）を返す。graphId は definition の display_id。</summary>
    [HttpGet("{graphId}")]
    [ProducesResponseType(typeof(GraphDefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphDefinitionResponse>> GetByGraphId(
        [Required(ErrorMessage = "graphId is required")]
        [NotWhitespace(ErrorMessage = "graphId is required")]
        string graphId,
        CancellationToken ct = default)
    {
        var graph = await _graphService.GetByGraphIdAsync(graphId, ct).ConfigureAwait(false);
        return Ok(graph);
    }
}
