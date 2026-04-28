using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/definitions/schema")]
public class DefinitionSchemasController : ControllerBase
{
    private readonly IDefinitionSchemaService _definitionSchemas;

    public DefinitionSchemasController(IDefinitionSchemaService definitionSchemas)
    {
        _definitionSchemas = definitionSchemas;
    }

    /// <summary>
    /// GET /v1/definitions/schema/nodes — nodes 形式の入力スキーマを返す。
    /// UI の補完/Lint 源泉として利用する。
    /// </summary>
    [HttpGet("nodes")]
    public ActionResult<DefinitionNodesSchemaResponse> GetNodesSchema()
    {
        return Ok(new DefinitionNodesSchemaResponse
        {
            SchemaVersion = _definitionSchemas.GetNodesSchemaVersion(),
            NodesVersion = _definitionSchemas.GetNodesVersion(),
            Schema = _definitionSchemas.GetNodesSchemaDocument()
        });
    }
}

public class DefinitionNodesSchemaResponse
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("nodesVersion")]
    public int NodesVersion { get; set; }

    [JsonPropertyName("schema")]
    public object Schema { get; set; } = new { };
}
