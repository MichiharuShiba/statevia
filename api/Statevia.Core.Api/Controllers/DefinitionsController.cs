using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/definitions")]
public class DefinitionsController : ControllerBase
{
    private const string DefaultTenantId = "default";

    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;

    public DefinitionsController(
        IDbContextFactory<CoreDbContext> dbFactory,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler)
    {
        _dbFactory = dbFactory;
        _displayIds = displayIds;
        _compiler = compiler;
    }

    /// <summary>POST /v1/definitions — 定義を登録。name + yaml を受け取り、検証・コンパイルして保存。</summary>
    [HttpPost]
    public async Task<ActionResult<DefinitionResponse>> Create([FromBody] CreateDefinitionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request?.Yaml))
            return ApiErrorResult.ValidationError("name and yaml are required");

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name, request.Yaml);
        }
        catch (ArgumentException ex)
        {
            return ApiErrorResult.ValidationError(ex.Message);
        }

        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var id = Guid.NewGuid();
        var displayId = await _displayIds.AllocateAsync("definition", id, ct).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.WorkflowDefinitions.Add(new WorkflowDefinitionRow
        {
            DefinitionId = id,
            TenantId = tenantId,
            Name = request.Name,
            SourceYaml = request.Yaml,
            CompiledJson = compiledJson,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = displayId }, new DefinitionResponse
        {
            DisplayId = displayId,
            ResourceId = id,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>GET /v1/definitions — 一覧（U4 一覧も display_id / resource_id）。display_ids を LEFT JOIN で 1 クエリ取得。</summary>
    [HttpGet]
    public async Task<ActionResult<List<DefinitionResponse>>> List(CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var displayIdsForDefinition = db.DisplayIds.Where(x => x.Kind == "definition");
        var list = await (
            from def in db.WorkflowDefinitions.AsNoTracking().Where(x => x.TenantId == tenantId)
            join d in displayIdsForDefinition on def.DefinitionId equals d.ResourceId into dGroup
            from d in dGroup.DefaultIfEmpty()
            orderby def.CreatedAt
            select new DefinitionResponse
            {
                DisplayId = d != null ? d.DisplayId : def.DefinitionId.ToString(),
                ResourceId = def.DefinitionId,
                Name = def.Name,
                CreatedAt = def.CreatedAt
            }).ToListAsync(ct).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Get(string id, CancellationToken ct)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? DefaultTenantId;
        var uuid = await _displayIds.ResolveAsync("definition", id, ct).ConfigureAwait(false);
        if (uuid is null)
            return ApiErrorResult.NotFound("Definition not found");

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == uuid.Value && x.TenantId == tenantId, ct).ConfigureAwait(false);
        if (row is null)
            return ApiErrorResult.NotFound("Definition not found");

        var displayId = await _displayIds.GetDisplayIdAsync("definition", id, ct).ConfigureAwait(false);

        return Ok(new DefinitionResponse
        {
            DisplayId = displayId ?? row.DefinitionId.ToString(),
            ResourceId = row.DefinitionId,
            Name = row.Name,
            CreatedAt = row.CreatedAt
        });
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
