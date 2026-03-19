using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly IExecutionReadModelService _executionReadModel;
    private readonly IWorkflowService _workflows;

    public WorkflowsController(
        IExecutionReadModelService executionReadModel,
        IWorkflowService workflows)
    {
        _executionReadModel = executionReadModel;
        _workflows = workflows;
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

    /// <summary>GET /v1/workflows — 一覧（U4 一覧も display_id / resource_id）。X-Tenant-Id でスコープ。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkflowResponse>>> List(CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var list = await _workflows.ListAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>GET /v1/workflows/{id} — Execution Read Model（契約準拠）を返す。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ExecutionReadModel>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var model = await _executionReadModel.GetByDisplayIdAsync(id, tenantId, ct).ConfigureAwait(false);
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
