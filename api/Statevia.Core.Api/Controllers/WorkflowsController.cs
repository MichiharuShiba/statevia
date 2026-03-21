using System;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/workflows")]
public class WorkflowsController : ControllerBase
{
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
    public async Task<ActionResult<WorkflowResponse>> Create([FromBody] StartWorkflowRequest request, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        var created = await _workflows.StartAsync(
            tenantId,
            request,
            idempotencyKey,
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
        CancellationToken ct = default)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
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
    public async Task<ActionResult<WorkflowResponse>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var model = await _workflows.GetWorkflowResponseAsync(tenantId, id, ct).ConfigureAwait(false);
        return Ok(model);
    }

    /// <summary>GET /v1/workflows/{id}/graph — execution_graph_snapshots から取得。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}/graph")]
    public async Task<ActionResult<string>> GetGraph(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var graphJson = await _workflows.GetGraphJsonAsync(tenantId, id, ct).ConfigureAwait(false);
        return Content(graphJson, "application/json");
    }

    /// <summary>GET /v1/workflows/{id}/state?atSeq= — UI 用 WorkflowView（リプレイは現状スナップショット近似）。</summary>
    [HttpGet("{id}/state")]
    public async Task<ActionResult<WorkflowViewDto>> GetState(string id, [FromQuery] long atSeq, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var view = await _workflows.GetWorkflowViewAtSeqAsync(tenantId, id, atSeq, ct).ConfigureAwait(false);
        return Ok(view);
    }

    /// <summary>GET /v1/workflows/{id}/events — event_store 由来のタイムライン（limit / afterSeq）。</summary>
    [HttpGet("{id}/events")]
    public async Task<ActionResult<ExecutionEventsResponseDto>> GetEvents(
        string id,
        [FromQuery] long afterSeq = 0,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var res = await _workflows.ListEventsAsync(tenantId, id, afterSeq, limit, ct).ConfigureAwait(false);
        return Ok(res);
    }

    /// <summary>GET /v1/workflows/{id}/stream — SSE（グラフ JSON の変化を GraphUpdated として送出）。</summary>
    [HttpGet("{id}/stream")]
    public async Task GetStream(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        await _stream.WriteStreamAsync(Response, tenantId, id, ct).ConfigureAwait(false);
    }

    /// <summary>POST /v1/workflows/{id}/cancel — Engine.CancelAsync を呼び、projection を更新。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        await _workflows.CancelAsync(
            tenantId,
            id,
            idempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/nodes/{nodeId}/resume — body: { "resumeKey": "..." }。PublishEvent と同様。X-Idempotency-Key で冪等。</summary>
    [HttpPost("{id}/nodes/{nodeId}/resume")]
    public async Task<ActionResult> ResumeNode(string id, string nodeId, [FromBody] ResumeNodeRequest? body, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        await _workflows.ResumeNodeAsync(
            tenantId,
            id,
            nodeId,
            body?.ResumeKey,
            idempotencyKey,
            Request.Method,
            Request.Path.Value ?? string.Empty,
            ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/events — body: { "name": "Approve" }。Engine.PublishEvent(workflowId, eventName)。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/events")]
    public async Task<ActionResult> PublishEvent(string id, [FromBody] PublishEventRequest body, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        await _workflows.PublishEventAsync(
            tenantId,
            id,
            body.Name,
            idempotencyKey,
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
}

public class PublishEventRequest
{
    [Required]
    public string Name { get; set; } = "";
}

public class WorkflowResponse
{
    public string DisplayId { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool CancelRequested { get; set; }
    public bool RestartLost { get; set; }
}
