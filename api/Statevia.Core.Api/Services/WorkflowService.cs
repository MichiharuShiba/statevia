using System.Text.Json;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Services;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IWorkflowEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly ICommandDedupService _dedupService;
    private readonly IWorkflowRepository _workflows;
    private readonly IDefinitionRepository _definitions;
    private readonly ICommandDedupRepository _dedup;

    public WorkflowService(
        IWorkflowEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        ICommandDedupService dedupService,
        IWorkflowRepository workflows,
        IDefinitionRepository definitions,
        ICommandDedupRepository dedup)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _dedupService = dedupService;
        _workflows = workflows;
        _definitions = definitions;
        _dedup = dedup;
    }

    public async Task<WorkflowResponse> StartAsync(
        string tenantId,
        StartWorkflowRequest request,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct)
    {
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, method, path);

        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                var cached = DeserializeCachedWorkflowResponse(existing);
                if (cached is not null)
                    return cached;
            }
        }

        var defUuid = await _displayIds.ResolveAsync("definition", request.DefinitionId!, ct).ConfigureAwait(false);
        if (defUuid is null)
            throw new NotFoundException("Definition not found");

        var defRow = await _definitions.GetByIdAsync(tenantId, defUuid.Value, ct).ConfigureAwait(false);
        if (defRow is null)
            throw new NotFoundException("Definition not found");

        var (compiled, _) = _compiler.ValidateAndCompile(defRow.Name, defRow.SourceYaml);

        var workflowId = _idGenerator.NewGuid();
        var engineId = workflowId.ToString();
        _engine.Start(compiled, engineId);

        var displayId = await _displayIds.AllocateAsync("workflow", workflowId, ct).ConfigureAwait(false);
        var status = MapStatus(_engine.GetSnapshot(engineId));
        var graphJson = _engine.ExportExecutionGraph(engineId);

        var createdAt = DateTime.UtcNow;

        await _workflows.AddWorkflowAndSnapshotAsync(
            new WorkflowRow
            {
                WorkflowId = workflowId,
                TenantId = tenantId,
                DefinitionId = defUuid.Value,
                Status = status,
                StartedAt = createdAt,
                UpdatedAt = createdAt,
                CancelRequested = false,
                RestartLost = false
            },
            new ExecutionGraphSnapshotRow
            {
                WorkflowId = workflowId,
                GraphJson = graphJson,
                UpdatedAt = createdAt
            },
            ct).ConfigureAwait(false);

        var response = new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflowId,
            Status = status,
            StartedAt = createdAt
        };

        if (dedupKey is { } saveKey)
        {
            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _dedup.SaveAsync(new CommandDedupRow
            {
                DedupKey = saveKey.DedupKey,
                Endpoint = saveKey.Endpoint,
                IdempotencyKey = saveKey.IdempotencyKey,
                RequestHash = null,
                StatusCode = StatusCodes.Status201Created,
                ResponseBody = responseJson,
                CreatedAt = createdAt,
                ExpiresAt = createdAt.AddHours(24)
            }, ct).ConfigureAwait(false);
        }
        return response;
    }

    public async Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct)
    {
        var pairs = await _workflows.ListWithDisplayIdsAsync(tenantId, ct).ConfigureAwait(false);
        return pairs.Select(p => new WorkflowResponse
        {
            DisplayId = p.DisplayId ?? p.Workflow.WorkflowId.ToString(),
            ResourceId = p.Workflow.WorkflowId,
            Status = p.Workflow.Status,
            StartedAt = p.Workflow.StartedAt,
            UpdatedAt = p.Workflow.UpdatedAt,
            CancelRequested = p.Workflow.CancelRequested,
            RestartLost = p.Workflow.RestartLost
        }).ToList();
    }

    public async Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        var row = await _workflows.GetSnapshotByWorkflowIdAsync(uuid.Value, ct).ConfigureAwait(false);
        return row is null ? throw new NotFoundException("Workflow not found") : row.GraphJson;
    }

    public async Task CancelAsync(
        string tenantId,
        string idOrUuid,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct)
    {
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, method, path);

        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
                return;
        }

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        await _engine.CancelAsync(uuid.Value.ToString()).ConfigureAwait(false);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (dedupKey is { } saveKey)
        {
            var now = DateTime.UtcNow;
            await _dedup.SaveAsync(new CommandDedupRow
            {
                DedupKey = saveKey.DedupKey,
                Endpoint = saveKey.Endpoint,
                IdempotencyKey = saveKey.IdempotencyKey,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            }, ct).ConfigureAwait(false);
        }
    }

    public async Task PublishEventAsync(
        string tenantId,
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct)
    {
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, method, path);

        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
                return;
        }

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        _engine.PublishEvent(uuid.Value.ToString(), eventName);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (dedupKey is { } saveKey)
        {
            var now = DateTime.UtcNow;
            await _dedup.SaveAsync(new CommandDedupRow
            {
                DedupKey = saveKey.DedupKey,
                Endpoint = saveKey.Endpoint,
                IdempotencyKey = saveKey.IdempotencyKey,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            }, ct).ConfigureAwait(false);
        }
    }

    private WorkflowResponse? DeserializeCachedWorkflowResponse(CommandDedupRow existing)
    {
        if (string.IsNullOrEmpty(existing.ResponseBody))
            return null;
        try
        {
            return JsonSerializer.Deserialize<WorkflowResponse>(
                existing.ResponseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
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
        await _workflows.UpdateWorkflowAndSnapshotAsync(
            workflowId,
            status,
            snapshot?.IsCancelled,
            graphJson,
            ct).ConfigureAwait(false);
    }
}

