using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Controllers;

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
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<DefinitionResponse>> Create(
        [FromBody] CreateDefinitionRequest request,
        CancellationToken ct = default)
    {
        var created = await _definitions.CreateAsync(request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>PUT /v1/definitions/{id} — 定義を更新。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Update(
        string id,
        [FromBody] UpdateDefinitionRequest request,
        CancellationToken ct = default)
    {
        var updated = await _definitions.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>
    /// GET /v1/definitions — ページング一覧（U4）。<c>limit</c> は必須。
    /// <c>?limit=&amp;offset=&amp;name=&amp;sortBy=&amp;sortOrder=</c> で <see cref="PagedResult{T}"/>（name は部分一致）。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DefinitionListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var offset = query.Offset ?? 0;
        var pageQuery = new DefinitionListPageQuery(
            Page: new PageQuery(offset, query.Limit!.Value),
            Sort: new SortQuery(query.SortBy, query.SortOrder),
            NameContains: query.Name,
            IncludeDeleted: query.IncludeDeleted ?? false);

        var paged = await _definitions.ListPagedAsync(pageQuery, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Get(
        string id,
        CancellationToken ct = default)
    {
        var row = await _definitions.GetAsync(id, ct).ConfigureAwait(false);
        return Ok(row);
    }

    /// <summary>DELETE /v1/definitions/{id} — catalog から論理削除する。</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        await _definitions.DeleteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/definitions/{id}/restore — 削除済み定義を復元する。</summary>
    [HttpPost("{id}/restore")]
    [ProducesResponseType(typeof(DefinitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DefinitionResponse>> Restore(string id, CancellationToken ct = default)
    {
        var restored = await _definitions.RestoreAsync(id, ct).ConfigureAwait(false);
        return Ok(restored);
    }
}
