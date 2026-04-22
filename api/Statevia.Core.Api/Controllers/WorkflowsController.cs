using System;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/workflows")]
public class WorkflowsController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "X-Idempotency-Key";

    private readonly IWorkflowService _workflows;
    private readonly WorkflowStreamService _stream;

    public WorkflowsController(
        IWorkflowService workflows,
        WorkflowStreamService stream)
    {
        _workflows = workflows;
        _stream = stream;
    }

    /// <summary>POST /v1/workflows — definitionId で定義を取得し、Engine.Start を呼ぶ。display_id と resource_id を返す（U4）。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowResponse>> Create(
        [FromBody] StartWorkflowRequest request,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var resolvedIdempotencyKey = idempotencyKey;
        var created = await _workflows.StartAsync(
            tenantId,
            request,
            resolvedIdempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = created.DisplayId }, created);
    }

    /// <summary>
    /// GET /v1/workflows — 一覧（U4）。クエリなしは従来どおり配列。
    /// <c>?limit=&amp;offset=&amp;status=</c> で <see cref="PagedResult{T}"/>（O1/O2）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] int offset = 0,
        [FromQuery] string? status = null,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        if (limit is null)
        {
            var list = await _workflows.ListAsync(tenantId, ct).ConfigureAwait(false);
            return Ok(list);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
        if (limit.Value > 500)
            throw new ArgumentException("limit must be at most 500");

        var paged = await _workflows.ListPagedAsync(tenantId, offset, limit.Value, status, ct).ConfigureAwait(false);
        return Ok(paged);
    }

    /// <summary>GET /v1/workflows/{id} — 一覧と同一の <see cref="WorkflowResponse"/>（UI WorkflowDTO 向け）。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowResponse>> Get(
        string id,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var model = await _workflows.GetWorkflowResponseAsync(tenantId, id, ct).ConfigureAwait(false);
        return Ok(model);
    }

    /// <summary>GET /v1/workflows/{id}/graph — execution_graph_snapshots から取得。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}/graph")]
    public async Task<ActionResult<string>> GetGraph(
        string id,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var graphJson = await _workflows.GetGraphJsonAsync(tenantId, id, ct).ConfigureAwait(false);
        return Content(graphJson, "application/json");
    }

    /// <summary>GET /v1/workflows/{id}/state?atSeq= — UI 用 WorkflowView（リプレイは現状スナップショット近似）。</summary>
    [HttpGet("{id}/state")]
    public async Task<ActionResult<WorkflowViewDto>> GetState(
        string id,
        [FromQuery] long atSeq,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var view = await _workflows.GetWorkflowViewAtSeqAsync(tenantId, id, atSeq, ct).ConfigureAwait(false);
        return Ok(view);
    }

    /// <summary>GET /v1/workflows/{id}/events — event_store 由来のタイムライン（limit / afterSeq）。</summary>
    [HttpGet("{id}/events")]
    public async Task<ActionResult<ExecutionEventsResponseDto>> GetEvents(
        string id,
        [FromQuery] long afterSeq = 0,
        [FromQuery] int limit = 500,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var res = await _workflows.ListEventsAsync(tenantId, id, afterSeq, limit, ct).ConfigureAwait(false);
        return Ok(res);
    }

    /// <summary>GET /v1/workflows/{id}/stream — SSE（グラフ JSON の変化を GraphUpdated として送出）。</summary>
    [HttpGet("{id}/stream")]
    public async Task GetStream(
        string id,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        await _stream.WriteStreamAsync(Response, tenantId, id, ct).ConfigureAwait(false);
    }

    /// <summary>POST /v1/workflows/{id}/cancel — Engine.CancelAsync を呼び、projection を更新。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(
        string id,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var resolvedIdempotencyKey = idempotencyKey;
        await _workflows.CancelAsync(
            tenantId,
            id,
            resolvedIdempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/nodes/{nodeId}/resume — body: { "resumeKey": "..." }。PublishEvent と同様。X-Idempotency-Key で冪等。</summary>
    [HttpPost("{id}/nodes/{nodeId}/resume")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public async Task<ActionResult> ResumeNode(
        string id,
        string nodeId,
        [FromBody] ResumeNodeRequest? body,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var resolvedIdempotencyKey = idempotencyKey;
        await _workflows.ResumeNodeAsync(
            tenantId,
            id,
            nodeId,
            body?.ResumeKey,
            resolvedIdempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/events — body: { "name": "Approve" }。Engine.PublishEvent(workflowId, eventName)。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/events")]
    public async Task<ActionResult> PublishEvent(
        string id,
        [FromBody] PublishEventRequest body,
        [FromHeader(Name = TenantHeader.HeaderName)] string? tenantIdHeader = null,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var tenantId = tenantIdHeader ?? TenantHeader.DefaultTenantId;
        var resolvedIdempotencyKey = idempotencyKey;
        await _workflows.PublishEventAsync(
            tenantId,
            id,
            body.Name,
            resolvedIdempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);
        return NoContent();
    }
}

public class StartWorkflowRequest
{
    [Required]
    public string DefinitionId { get; set; } = "";
    public JsonElement? Input { get; set; }
}

public class PublishEventRequest
{
    [Required]
    public string Name { get; set; } = "";
}

public class WorkflowResponse
{
    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; } = "";

    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("cancelRequested")]
    public bool CancelRequested { get; set; }

    [JsonPropertyName("restartLost")]
    public bool RestartLost { get; set; }
}
