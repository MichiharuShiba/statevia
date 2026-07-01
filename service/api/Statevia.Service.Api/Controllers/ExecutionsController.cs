using System;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// 実行 REST API（<c>/v1/executions</c>）。
/// </summary>
[ApiController]
[Route("v1/executions")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S6960:Controllers should not have mixed responsibilities",
    Justification = "実行 CRUD と SSE は同一リソース境界のため当面は単一コントローラで維持する。")]
public class ExecutionsController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "X-Idempotency-Key";

    private readonly IExecutionService _executions;
    private readonly ExecutionStreamService _stream;

    /// <summary>
    /// <see cref="ExecutionsController"/> を生成する。
    /// </summary>
    /// <param name="executions">実行サービス。</param>
    /// <param name="stream">SSE 用ストリームサービス。</param>
    public ExecutionsController(
        IExecutionService executions,
        ExecutionStreamService stream)
    {
        _executions = executions;
        _stream = stream;
    }

    /// <summary>POST /v1/executions — definitionId で定義を取得し、Engine.Start を呼ぶ。display_id と resource_id を返す（U4）。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExecutionResponse>> Create(
        [FromBody] StartExecutionRequest request,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var created = await _executions.StartAsync(
            request,
            idempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>
    /// GET /v1/executions — ページング一覧（U4）。<c>limit</c> は必須。
    /// <c>?limit=&amp;offset=&amp;status=&amp;definitionId=&amp;name=&amp;sortBy=&amp;sortOrder=</c> で <see cref="PagedResult{T}"/>（O1/O2）。
    /// <c>definitionId</c> は定義の display / UUID。 <c>name</c> は execution の <c>displayId</c> 部分一致、または execution の UUID 完全一致で絞り込み。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExecutionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ExecutionListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is null)
            throw new ArgumentException("limit is required");

        var offset = query.Offset ?? 0;
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit.Value, 1);
        if (query.Limit.Value > 500)
            throw new ArgumentException("limit must be at most 500");

        var paged = await _executions.ListPagedAsync(query, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/executions/{id} — 一覧と同一の <see cref="ExecutionResponse"/>（UI ExecutionDTO 向け）。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionResponse>> Get(
        string id,
        CancellationToken ct = default)
    {
        var model = await _executions.GetExecutionResponseAsync(id, ct).ConfigureAwait(false);
        return Ok(model);
    }

    /// <summary>GET /v1/executions/{id}/graph — execution_graph_snapshots から取得。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}/graph")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<string>> GetGraph(
        string id,
        CancellationToken ct = default)
    {
        var graphJson = await _executions.GetGraphJsonAsync(id, ct).ConfigureAwait(false);
        return Content(graphJson, "application/json");
    }

    /// <summary>GET /v1/executions/{id}/state?atSeq= — UI 用 ExecutionView（リプレイは現状スナップショット近似）。</summary>
    [HttpGet("{id}/state")]
    [ProducesResponseType(typeof(ExecutionViewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionViewDto>> GetState(
        string id,
        [FromQuery] long atSeq,
        CancellationToken ct = default)
    {
        var view = await _executions.GetExecutionViewAtSeqAsync(id, atSeq, ct).ConfigureAwait(false);
        return Ok(view);
    }

    /// <summary>GET /v1/executions/{id}/events — event_store 由来のタイムライン（limit / afterSeq）。</summary>
    [HttpGet("{id}/events")]
    [ProducesResponseType(typeof(ExecutionEventsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionEventsResponseDto>> GetEvents(
        string id,
        [FromQuery] long afterSeq = 0,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        var res = await _executions.ListEventsAsync(id, afterSeq, limit, ct).ConfigureAwait(false);
        return Ok(res);
    }

    /// <summary>GET /v1/executions/{id}/stream — SSE（グラフ JSON の変化を GraphUpdated として送出）。</summary>
    [HttpGet("{id}/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task GetStream(
        string id,
        CancellationToken ct = default)
    {
        await _stream.WriteStreamAsync(Response, id, ct).ConfigureAwait(false);
    }

    /// <summary>POST /v1/executions/{id}/cancel — Engine.CancelAsync を呼び、projection を更新。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Cancel(
        string id,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.CancelAsync(
            id,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>POST /v1/executions/{id}/nodes/{nodeId}/resume — body: { "resumeKey": "..." }。PublishEvent と同様。X-Idempotency-Key で冪等。</summary>
    [HttpPost("{id}/nodes/{nodeId}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public async Task<ActionResult> ResumeNode(
        string id,
        string nodeId,
        [FromBody] ResumeNodeRequest? body,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.ResumeNodeAsync(
            id,
            nodeId,
            body?.ResumeKey,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/executions/{id}/events — body: { "name": "Approve" }。Engine.PublishEvent(executionId, eventName)。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/events")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> PublishEvent(
        string id,
        [FromBody] PublishEventRequest body,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        var resolvedIdempotencyKey = idempotencyKey;
        await _executions.PublishEventAsync(
            id,
            body.Name,
            resolvedIdempotencyKey,
            new CommandRequestContext(Request.Method, Request.Path.Value ?? string.Empty),
            ct).ConfigureAwait(false);
        return NoContent();
    }
}

/// <summary>POST /v1/executions のリクエスト本文。</summary>
public class StartExecutionRequest
{
    /// <summary>開始に用いる定義 ID（display または UUID）。</summary>
    [Required]
    public string DefinitionId { get; set; } = "";

    /// <summary>開始に用いる版番号（省略時は latestVersion）。</summary>
    [JsonPropertyName("definitionVersion")]
    public int? DefinitionVersion { get; set; }

    /// <summary>開始に用いる版 UUID（definitionVersion より優先）。</summary>
    [JsonPropertyName("definitionVersionId")]
    public Guid? DefinitionVersionId { get; set; }

    /// <summary>実行開始時の入力（任意）。</summary>
    public JsonElement? Input { get; set; }
}

/// <summary>POST …/events のリクエスト本文。</summary>
public class PublishEventRequest
{
    /// <summary>発行するイベント名。</summary>
    [Required]
    public string Name { get; set; } = "";
}

/// <summary>ワークフロー一覧・単票の JSON 応答形（U4）。</summary>
public class ExecutionResponse
{
    /// <summary>表示用ワークフロー ID。</summary>
    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = "";

    /// <summary>ワークフローのリソース UUID。</summary>
    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    /// <summary>定義グラフ ID。</summary>
    [JsonPropertyName("graphId")]
    public string GraphId { get; set; } = "";

    /// <summary>状態文字列。</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>開始日時（UTC）。</summary>
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    /// <summary>最終更新日時（UTC）。</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>キャンセル要求フラグ。</summary>
    [JsonPropertyName("cancelRequested")]
    public bool CancelRequested { get; set; }

    /// <summary>再起動喪失フラグ。</summary>
    [JsonPropertyName("restartLost")]
    public bool RestartLost { get; set; }
}