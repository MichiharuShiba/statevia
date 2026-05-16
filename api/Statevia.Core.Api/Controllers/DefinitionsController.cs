using System;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

/// <summary>
/// ワークフロー定義 REST API（<c>/v1/definitions</c>）。
/// </summary>
[ApiController]
[Route("v1/definitions")]
public class DefinitionsController : ControllerBase
{
    private readonly IDefinitionService _definitions;

    /// <summary>
    /// <see cref="DefinitionsController"/> を生成する。
    /// </summary>
    /// <param name="definitions">定義サービス。</param>
    public DefinitionsController(
        IDefinitionService definitions)
    {
        _definitions = definitions;
    }

    /// <summary>POST /v1/definitions — 定義を登録。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    [HttpPost]
    public async Task<ActionResult<DefinitionResponse>> Create(
        [FromBody] CreateDefinitionRequest request,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var created = await _definitions.CreateAsync(tenantId, request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>PUT /v1/definitions/{id} — 定義を更新。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Update(
        string id,
        [FromBody] UpdateDefinitionRequest request,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var updated = await _definitions.UpdateAsync(tenantId, id, request, ct).ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>
    /// GET /v1/definitions — 一覧（U4）。クエリなしは従来どおり配列。
    /// <c>?limit=&amp;offset=&amp;name=&amp;sortBy=&amp;sortOrder=</c> で <see cref="PagedResult{T}"/>（name は部分一致）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DefinitionListQuery query,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        if (query.Limit is null)
        {
            var list = await _definitions.ListAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(list);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(query.Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit.Value, 1);
        if (query.Limit.Value > 500)
            throw new ArgumentException("limit must be at most 500");

        var paged = await _definitions.ListPagedAsync(tenantId, query, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Get(
        string id,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var row = await _definitions.GetAsync(tenantId, id, ct).ConfigureAwait(false);
        return Ok(row);
    }

}

/// <summary>POST /v1/definitions のリクエスト本文。</summary>
public class CreateDefinitionRequest
{
    /// <summary>定義名。</summary>
    [Required]
    public string Name { get; set; } = "";

    /// <summary>定義ソース YAML。</summary>
    [Required]
    public string Yaml { get; set; } = "";
}

/// <summary>PUT /v1/definitions/{id} のリクエスト本文。</summary>
public class UpdateDefinitionRequest
{
    /// <summary>定義名。</summary>
    [Required]
    public string Name { get; set; } = "";

    /// <summary>定義ソース YAML。</summary>
    [Required]
    public string Yaml { get; set; } = "";
}

/// <summary>定義の JSON 応答形（U4）。</summary>
public class DefinitionResponse
{
    /// <summary>表示用定義 ID。</summary>
    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = "";

    /// <summary>定義のリソース UUID。</summary>
    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    /// <summary>定義名。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>作成日時（UTC）。</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>更新日時（UTC）。</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>ソース YAML（取得時のみ。任意）。</summary>
    [JsonPropertyName("yaml")]
    public string? Yaml { get; set; }
}
