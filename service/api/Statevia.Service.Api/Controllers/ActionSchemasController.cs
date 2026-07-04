using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// Action Schema REST API（<c>/v1/actions/schema</c>）。
/// </summary>
[ApiController]
[Route("v1/actions/schema")]
public class ActionSchemasController : ControllerBase
{
    private readonly IActionSchemaService _actionSchemas;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;

    /// <summary>
    /// <see cref="ActionSchemasController"/> を生成する。
    /// </summary>
    /// <param name="actionSchemas">Action Schema サービス。</param>
    /// <param name="runtimeAuth">Runtime permission 認可。</param>
    public ActionSchemasController(
        IActionSchemaService actionSchemas,
        IRuntimePermissionAuthorization runtimeAuth)
    {
        _actionSchemas = actionSchemas;
        _runtimeAuth = runtimeAuth;
    }

    /// <summary>
    /// GET /v1/actions/schema — 登録 action 一覧と descriptor 概要を返す。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ActionSchemaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionSchemaListResponse>> GetList(CancellationToken cancellationToken)
    {
        await _runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, cancellationToken)
            .ConfigureAwait(false);

        return Ok(_actionSchemas.GetList());
    }

    /// <summary>
    /// GET /v1/actions/schema/index — Playground 向け軽量 index を返す。
    /// </summary>
    [HttpGet("index")]
    [ProducesResponseType(typeof(ActionSchemaIndexResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionSchemaIndexResponse>> GetIndex(CancellationToken cancellationToken)
    {
        await _runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, cancellationToken)
            .ConfigureAwait(false);

        return Ok(_actionSchemas.GetIndex());
    }

    /// <summary>
    /// GET /v1/actions/schema/{actionId} — input/output schema と UI metadata を返す。
    /// </summary>
    [HttpGet("{actionId}")]
    [ProducesResponseType(typeof(ActionSchemaDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionSchemaDetailResponse>> GetDetail(
        string actionId,
        CancellationToken cancellationToken)
    {
        await _runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, cancellationToken)
            .ConfigureAwait(false);

        return Ok(_actionSchemas.GetDetail(actionId));
    }
}
