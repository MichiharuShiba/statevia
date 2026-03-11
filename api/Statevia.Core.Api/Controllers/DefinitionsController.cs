using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Controllers;

[ApiController]
[Route("v1/definitions")]
public class DefinitionsController : ControllerBase
{
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
            return BadRequest(new { error = "name and yaml are required" });

        string compiledJson;
        try
        {
            (_, compiledJson) = _compiler.ValidateAndCompile(request.Name, request.Yaml);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var id = Guid.NewGuid();
        var displayId = await _displayIds.AllocateAsync("definition", id, ct).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.WorkflowDefinitions.Add(new WorkflowDefinitionRow
        {
            DefinitionId = id,
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

    /// <summary>GET /v1/definitions/{id} — 表示用 ID または UUID で取得（U4）。</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DefinitionResponse>> Get(string id, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("definition", id, ct).ConfigureAwait(false);
        if (uuid == null)
            return NotFound();

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.DefinitionId == uuid.Value, ct).ConfigureAwait(false);
        if (row == null)
            return NotFound();

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
