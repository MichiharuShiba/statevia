using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/definitions")]
public class DefinitionsController : ControllerBase
{
    private readonly IDefinitionService _definitions;

    public DefinitionsController(
        IDefinitionService definitions)
    {
        _definitions = definitions;
    }

    /// <summary>POST /v1/definitions — 定義を登録。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    [HttpPost]
    public async Task<ActionResult<DefinitionResponse>> Create([FromBody] CreateDefinitionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request?.Yaml))
            return ApiErrorResult.ValidationError("name and yaml are required");

        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;

        try
        {
            var created = await _definitions.CreateAsync(tenantId, request, ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
        }
        catch (ArgumentException ex)
        {
            return ApiErrorResult.ValidationError(ex.Message);
        }
    }

    /// <summary>GET /v1/definitions — 一覧（U4 一覧も display_id / resource_id）。display_ids を LEFT JOIN で 1 クエリ取得。</summary>
    [HttpGet]
    public async Task<ActionResult<List<DefinitionResponse>>> List(CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var list = await _definitions.ListAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var row = await _definitions.GetAsync(tenantId, id, ct).ConfigureAwait(false);
        return row is null ? ApiErrorResult.NotFound("Definition not found") : Ok(row);
    }
}

public class CreateDefinitionRequest
{
    public string? Name { get; set; }
    public string? Yaml { get; set; }
}

public class DefinitionResponse
{
    public string DisplayId { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
