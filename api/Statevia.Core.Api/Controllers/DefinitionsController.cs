using System;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;

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
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var created = await _definitions.CreateAsync(tenantId, request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>
    /// GET /v1/definitions — 一覧（U4）。クエリなしは従来どおり配列。
    /// <c>?limit=&amp;offset=&amp;name=</c> で <see cref="PagedResult{T}"/>（name は部分一致）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] int offset = 0,
        [FromQuery] string? name = null,
        CancellationToken ct = default)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        if (limit is null)
        {
            var list = await _definitions.ListAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(list);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
        if (limit.Value > 500)
            throw new ArgumentException("limit must be at most 500");

        var paged = await _definitions.ListPagedAsync(tenantId, offset, limit.Value, name, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var row = await _definitions.GetAsync(tenantId, id, ct).ConfigureAwait(false);
        return Ok(row);
    }
}

public class CreateDefinitionRequest
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string Yaml { get; set; } = "";
}

public class DefinitionResponse
{
    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = "";

    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
