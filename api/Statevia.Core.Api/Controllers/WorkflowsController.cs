using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowEngine _engine;
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;

    public WorkflowsController(
        IWorkflowEngine engine,
        IDbContextFactory<CoreDbContext> dbFactory,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler)
    {
        _engine = engine;
        _dbFactory = dbFactory;
        _displayIds = displayIds;
        _compiler = compiler;
    }

    /// <summary>POST /v1/workflows — definitionId で定義を取得し、Engine.Start を呼ぶ。display_id と resource_id を返す（U4）。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowResponse>> Create([FromBody] StartWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.DefinitionId))
            return BadRequest(new { error = "definitionId is required" });

        var defUuid = await _displayIds.ResolveAsync("definition", request.DefinitionId, ct).ConfigureAwait(false);
        if (defUuid == null)
            return NotFound(new { error = "definition not found" });

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var defRow = await db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.DefinitionId == defUuid.Value, ct).ConfigureAwait(false);
        if (defRow == null)
            return NotFound(new { error = "definition not found" });

        CompiledWorkflowDefinition compiled;
        try
        {
            (compiled, _) = _compiler.ValidateAndCompile(defRow.Name, defRow.SourceYaml);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var workflowId = Guid.NewGuid();
        var engineId = workflowId.ToString(); // Engine は string で ID を扱うので UUID 文字列を渡す
        _engine.Start(compiled, engineId);

        var displayId = await _displayIds.AllocateAsync("workflow", workflowId, ct).ConfigureAwait(false);

        var status = MapStatus(_engine.GetSnapshot(engineId));
        var graphJson = _engine.ExportExecutionGraph(engineId);

        db.Workflows.Add(new WorkflowRow
        {
            WorkflowId = workflowId,
            DefinitionId = defUuid.Value,
            Status = status,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        });
        db.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
        {
            WorkflowId = workflowId,
            GraphJson = graphJson,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = displayId }, new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflowId,
            Status = status,
            StartedAt = DateTime.UtcNow
        });
    }

    /// <summary>GET /v1/workflows — 一覧（U4 一覧も display_id / resource_id）。display_ids を LEFT JOIN で 1 クエリ取得。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkflowResponse>>> List(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "workflow");
        var list = await (
            from w in db.Workflows.AsNoTracking()
            join d in displayIdsForWorkflow on w.WorkflowId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            orderby w.StartedAt descending
            select new WorkflowResponse
            {
                DisplayId = d != null ? d.DisplayId : w.WorkflowId.ToString(),
                ResourceId = w.WorkflowId,
                Status = w.Status,
                StartedAt = w.StartedAt,
                UpdatedAt = w.UpdatedAt,
                CancelRequested = w.CancelRequested,
                RestartLost = w.RestartLost
            }).ToListAsync(ct).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>GET /v1/workflows/{id} — DB の projection から返す（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowResponse>> Get(string id, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid == null)
            return NotFound();

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.Workflows.AsNoTracking().FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value, ct).ConfigureAwait(false);
        if (row == null)
            return NotFound();

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", id, ct).ConfigureAwait(false);

        return Ok(new WorkflowResponse
        {
            DisplayId = displayId ?? row.WorkflowId.ToString(),
            ResourceId = row.WorkflowId,
            Status = row.Status,
            StartedAt = row.StartedAt,
            UpdatedAt = row.UpdatedAt,
            CancelRequested = row.CancelRequested,
            RestartLost = row.RestartLost
        });
    }

    /// <summary>GET /v1/workflows/{id}/graph — execution_graph_snapshots から取得。</summary>
    [HttpGet("{id}/graph")]
    public async Task<ActionResult<string>> GetGraph(string id, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid == null)
            return NotFound();

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.ExecutionGraphSnapshots.AsNoTracking().FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value, ct).ConfigureAwait(false);
        if (row == null)
            return NotFound();

        return Content(row.GraphJson, "application/json");
    }

    /// <summary>POST /v1/workflows/{id}/cancel — Engine.CancelAsync を呼び、projection を更新。</summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(string id, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid == null)
            return NotFound();

        var engineId = uuid.Value.ToString();
        await _engine.CancelAsync(engineId).ConfigureAwait(false);

        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/events — body: { "name": "Approve" }。Engine.PublishEvent(workflowId, eventName)。</summary>
    [HttpPost("{id}/events")]
    public async Task<ActionResult> PublishEvent(string id, [FromBody] PublishEventRequest? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Name))
            return BadRequest(new { error = "name is required" });

        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid == null)
            return NotFound();

        var engineId = uuid.Value.ToString();
        _engine.PublishEvent(engineId, body.Name);

        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);
        return NoContent();
    }

    private static string MapStatus(WorkflowSnapshot? snapshot)
    {
        if (snapshot == null) return "Unknown";
        if (snapshot.IsCompleted) return "Completed";
        if (snapshot.IsCancelled) return "Cancelled";
        if (snapshot.IsFailed) return "Failed";
        return "Running";
    }

    private async Task UpdateProjectionAsync(Guid workflowId, CancellationToken ct)
    {
        var engineId = workflowId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var w = await db.Workflows.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (w != null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            w.CancelRequested = snapshot?.IsCancelled ?? w.CancelRequested;
        }
        var g = await db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (g != null)
        {
            g.GraphJson = graphJson;
            g.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public class StartWorkflowRequest
{
    public string? DefinitionId { get; set; }
}

public class PublishEventRequest
{
    public string? Name { get; set; }
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
