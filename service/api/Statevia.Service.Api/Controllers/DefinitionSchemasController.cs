using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Security;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// nodes 入力スキーマ REST API（<c>/v1/definitions/schema</c>）。
/// </summary>
[ApiController]
[Route("v1/definitions/schema")]
public class DefinitionSchemasController : ControllerBase
{
    private readonly IDefinitionSchemaService _definitionSchemas;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;

    /// <summary>
    /// <see cref="DefinitionSchemasController"/> を生成する。
    /// </summary>
    /// <param name="definitionSchemas">スキーマサービス。</param>
    /// <param name="runtimeAuth">Runtime permission 認可。</param>
    public DefinitionSchemasController(
        IDefinitionSchemaService definitionSchemas,
        IRuntimePermissionAuthorization runtimeAuth)
    {
        _definitionSchemas = definitionSchemas;
        _runtimeAuth = runtimeAuth;
    }

    /// <summary>
    /// GET /v1/definitions/schema/nodes — nodes 形式の入力スキーマを返す。
    /// UI の補完/Lint 源泉として利用する。
    /// </summary>
    [HttpGet("nodes")]
    [ProducesResponseType(typeof(DefinitionNodesSchemaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionNodesSchemaResponse>> GetNodesSchema(CancellationToken cancellationToken)
    {
        await _runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.DefinitionsRead, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new DefinitionNodesSchemaResponse
        {
            SchemaVersion = _definitionSchemas.GetNodesSchemaVersion(),
            NodesVersion = _definitionSchemas.GetNodesVersion(),
            Schema = _definitionSchemas.GetNodesSchemaDocument()
        });
    }
}

/// <summary>GET …/schema/nodes のレスポンス本文。</summary>
public class DefinitionNodesSchemaResponse
{
    /// <summary>スキーマ文書のバージョン文字列。</summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "";

    /// <summary>nodes スキーマの整数バージョン。</summary>
    [JsonPropertyName("nodesVersion")]
    public int NodesVersion { get; set; }

    /// <summary>スキーマ JSON オブジェクト。</summary>
    [JsonPropertyName("schema")]
    public object Schema { get; set; } = new { };
}
