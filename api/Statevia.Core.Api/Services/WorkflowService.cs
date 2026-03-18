using System.Text.Json;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Services;

public interface IWorkflowService
{
    Task<WorkflowResponse?> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct);
    Task<string?> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<bool> CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task<bool> PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct);
}

public sealed class WorkflowService : IWorkflowService
{
    private readonly IWorkflowEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly ICommandDedupService _dedupService;
    private readonly IWorkflowRepository _workflows;
    private readonly IDefinitionRepository _definitions;
    private readonly ICommandDedupRepository _dedup;

    public WorkflowService(
        IWorkflowEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        ICommandDedupService dedupService,
        IWorkflowRepository workflows,
        IDefinitionRepository definitions,
        ICommandDedupRepository dedup)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _dedupService = dedupService;
        _workflows = workflows;
        _definitions = definitions;
        _dedup = dedup;
    }

    public async Task<WorkflowResponse?> StartAsync(
        string tenantId,
        StartWorkflowRequest request,
        CommandDedupKey? dedupKey,
        CancellationToken ct)
    {
        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                return DeserializeCachedWorkflowResponse(existing);
            }
        }

        var defUuid = await _displayIds.ResolveAsync("definition", request.DefinitionId!, ct).ConfigureAwait(false);
        if (defUuid is null)
            return null;

        var defRow = await _definitions.GetByIdAsync(tenantId, defUuid.Value, ct).ConfigureAwait(false);
        if (defRow is null)
            return null;

        var (compiled, _) = _compiler.ValidateAndCompile(defRow.Name, defRow.SourceYaml);

        var workflowId = Guid.NewGuid();
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
                IdempotencyKey = null,
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

    public async Task<string?> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            return null;

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            return null;

        var row = await _workflows.GetSnapshotByWorkflowIdAsync(uuid.Value, ct).ConfigureAwait(false);
        return row is null ? null : row.GraphJson;
    }

    public async Task<bool> CancelAsync(
        string tenantId,
        string idOrUuid,
        CommandDedupKey? dedupKey,
        CancellationToken ct)
    {
        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
                return true;
        }

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            return false;

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            return false;

        await _engine.CancelAsync(uuid.Value.ToString()).ConfigureAwait(false);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (dedupKey is { } saveKey)
        {
            var now = DateTime.UtcNow;
            await _dedup.SaveAsync(new CommandDedupRow
            {
                DedupKey = saveKey.DedupKey,
                Endpoint = saveKey.Endpoint,
                IdempotencyKey = null,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            }, ct).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<bool> PublishEventAsync(
        string tenantId,
        string idOrUuid,
        string eventName,
        CommandDedupKey? dedupKey,
        CancellationToken ct)
    {
        if (dedupKey is { } key)
        {
            var dedupCheckTime = DateTime.UtcNow;
            var existing = await _dedup.FindValidAsync(key.DedupKey, dedupCheckTime, ct).ConfigureAwait(false);
            if (existing is not null)
                return true;
        }

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            return false;

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            return false;

        _engine.PublishEvent(uuid.Value.ToString(), eventName);
        await UpdateProjectionAsync(uuid.Value, ct).ConfigureAwait(false);

        if (dedupKey is { } saveKey)
        {
            var now = DateTime.UtcNow;
            await _dedup.SaveAsync(new CommandDedupRow
            {
                DedupKey = saveKey.DedupKey,
                Endpoint = saveKey.Endpoint,
                IdempotencyKey = null,
                RequestHash = null,
                StatusCode = StatusCodes.Status204NoContent,
                ResponseBody = null,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            }, ct).ConfigureAwait(false);
        }

        return true;
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

