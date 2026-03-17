using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/workflows")]
public class WorkflowsController : ControllerBase
{
    private const string DefaultTenantId = "default";

    private readonly IWorkflowEngine _engine;
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IExecutionReadModelService _executionReadModel;

    public WorkflowsController(
        IWorkflowEngine engine,
        IDbContextFactory<CoreDbContext> dbFactory,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IExecutionReadModelService executionReadModel)
    {
        _engine = engine;
        _dbFactory = dbFactory;
        _displayIds = displayIds;
        _compiler = compiler;
        _executionReadModel = executionReadModel;
    }

    /// <summary>POST /v1/workflows — definitionId で定義を取得し、Engine.Start を呼ぶ。display_id と resource_id を返す（U4）。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowResponse>> Create([FromBody] StartWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.DefinitionId))
            return ApiErrorResult.ValidationError("definitionId is required");

        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();

        var defUuid = await _displayIds.ResolveAsync("definition", request.DefinitionId, ct).ConfigureAwait(false);
        if (defUuid is null)
            return ApiErrorResult.NotFound("Definition not found");

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var now = DateTime.UtcNow;
            var (dedupKey, endpoint) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);

            var existing = await db.CommandDedup.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.DedupKey == dedupKey && x.ExpiresAt > now,
                    ct)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return ReturnCachedIdempotentResponse(existing);
            }
        }

        var defRow = await db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == defUuid.Value && x.TenantId == tenantId, ct).ConfigureAwait(false);
        if (defRow is null)
            return ApiErrorResult.NotFound("Definition not found");

        CompiledWorkflowDefinition compiled;
        try
        {
            (compiled, _) = _compiler.ValidateAndCompile(defRow.Name, defRow.SourceYaml);
        }
        catch (ArgumentException ex)
        {
            return ApiErrorResult.ValidationError(ex.Message);
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
            TenantId = tenantId,
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

        var response = new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflowId,
            Status = status,
            StartedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var now = DateTime.UtcNow;
            var (dedupKey, endpoint) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var responseJson = JsonSerializer.Serialize(response, jsonOptions);

            db.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = endpoint,
                IdempotencyKey = idempotencyKey,
                // TODO: request_hash はリクエストボディのハッシュを計算して設定する。
                RequestHash = null,
                StatusCode = StatusCodes.Status201Created,
                ResponseBody = responseJson,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = displayId }, response);
    }

    /// <summary>GET /v1/workflows — 一覧（U4 一覧も display_id / resource_id）。X-Tenant-Id でスコープ。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkflowResponse>>> List(CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var displayIdsForWorkflow = db.DisplayIds.Where(x => x.Kind == "workflow");
        var list = await (
            from w in db.Workflows.AsNoTracking().Where(x => x.TenantId == tenantId)
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

    /// <summary>GET /v1/workflows/{id} — Execution Read Model（契約準拠）を返す。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ExecutionReadModel>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var model = await _executionReadModel.GetByDisplayIdAsync(id, tenantId, ct).ConfigureAwait(false);
        if (model is null)
            return ApiErrorResult.NotFound("Workflow not found");
        return Ok(model);
    }

    /// <summary>GET /v1/workflows/{id}/graph — execution_graph_snapshots から取得。X-Tenant-Id でスコープ。</summary>
    [HttpGet("{id}/graph")]
    public async Task<ActionResult<string>> GetGraph(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid is null)
            return ApiErrorResult.NotFound("Workflow not found");

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var workflow = await db.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value && x.TenantId == tenantId, ct).ConfigureAwait(false);
        if (workflow is null)
            return ApiErrorResult.NotFound("Workflow not found");
        var row = await db.ExecutionGraphSnapshots.AsNoTracking().FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value, ct).ConfigureAwait(false);
        if (row is null)
            return ApiErrorResult.NotFound("Workflow not found");

        return Content(row.GraphJson, "application/json");
    }

    /// <summary>POST /v1/workflows/{id}/cancel — Engine.CancelAsync を呼び、projection を更新。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();

        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid is null)
            return ApiErrorResult.NotFound("Workflow not found");

        await using var dbCheck = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var workflow = await dbCheck.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value && x.TenantId == tenantId, ct).ConfigureAwait(false);
        if (workflow is null)
            return ApiErrorResult.NotFound("Workflow not found");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await using var dbLookup = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            var (dedupKey, _) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);
            var existing = await dbLookup.CommandDedup.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DedupKey == dedupKey && x.ExpiresAt > now, ct)
                .ConfigureAwait(false);
            if (existing is not null)
                return StatusCode(existing.StatusCode ?? StatusCodes.Status204NoContent);
        }

        var engineId = uuid.Value.ToString();
        await _engine.CancelAsync(engineId).ConfigureAwait(false);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var now = DateTime.UtcNow;
            var (dedupKey, endpoint) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);
            await using var dbSave = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            dbSave.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = endpoint,
                IdempotencyKey = idempotencyKey,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            });
            await dbSave.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return NoContent();
    }

    /// <summary>POST /v1/workflows/{id}/events — body: { "name": "Approve" }。Engine.PublishEvent(workflowId, eventName)。X-Idempotency-Key で冪等。X-Tenant-Id でスコープ。</summary>
    [HttpPost("{id}/events")]
    public async Task<ActionResult> PublishEvent(string id, [FromBody] PublishEventRequest? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Name))
            return ApiErrorResult.ValidationError("name is required");

        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].FirstOrDefault();

        var uuid = await _displayIds.ResolveAsync("workflow", id, ct).ConfigureAwait(false);
        if (uuid is null)
            return ApiErrorResult.NotFound("Workflow not found");

        await using var dbCheck = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var workflow = await dbCheck.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkflowId == uuid.Value && x.TenantId == tenantId, ct).ConfigureAwait(false);
        if (workflow is null)
            return ApiErrorResult.NotFound("Workflow not found");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await using var dbLookup = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            var (dedupKey, _) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);
            var existing = await dbLookup.CommandDedup.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DedupKey == dedupKey && x.ExpiresAt > now, ct)
                .ConfigureAwait(false);
            if (existing is not null)
                return StatusCode(existing.StatusCode ?? StatusCodes.Status204NoContent);
        }

        var engineId = uuid.Value.ToString();
        _engine.PublishEvent(engineId, body.Name);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var now = DateTime.UtcNow;
            var (dedupKey, endpoint) = BuildDedupKeyAndEndpoint(tenantId, idempotencyKey);
            await using var dbSave = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            dbSave.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = endpoint,
                IdempotencyKey = idempotencyKey,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            });
            await dbSave.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return NoContent();
    }

    /// <summary>command_dedup に保存した初回レスポンスを返す。201 の場合は Location 付きで返す。</summary>
    private ActionResult<WorkflowResponse> ReturnCachedIdempotentResponse(CommandDedupRow existing)
    {
        var statusCode = existing.StatusCode ?? StatusCodes.Status201Created;
        if (string.IsNullOrEmpty(existing.ResponseBody))
        {
            return StatusCode(statusCode);
        }

        if (statusCode is StatusCodes.Status201Created)
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var cached = JsonSerializer.Deserialize<WorkflowResponse>(existing.ResponseBody, jsonOptions);
            if (cached is not null)
            {
                return CreatedAtAction(nameof(Get), new { id = cached.DisplayId }, cached);
            }
        }

        return new ContentResult
        {
            Content = existing.ResponseBody,
            ContentType = "application/json",
            StatusCode = statusCode
        };
    }

    /// <summary>Request から Method と Path を取得し、冪等用の dedup_key と endpoint を組み立てる。テナント単位で一意。</summary>
    private (string DedupKey, string Endpoint) BuildDedupKeyAndEndpoint(string tenantId, string idempotencyKey)
    {
        var path = Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var endpoint = $"{Request.Method} {path}";
        var dedupKey = $"{tenantId}|{endpoint}:{idempotencyKey}";
        return (dedupKey, endpoint);
    }

    private static string MapStatus(WorkflowSnapshot? snapshot)
    {
        if (snapshot is null) return "Unknown";
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
        if (w is not null)
        {
            w.Status = status;
            w.UpdatedAt = DateTime.UtcNow;
            w.CancelRequested = snapshot?.IsCancelled ?? w.CancelRequested;
        }
        var g = await db.ExecutionGraphSnapshots.FirstOrDefaultAsync(x => x.WorkflowId == workflowId, ct).ConfigureAwait(false);
        if (g is not null)
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
