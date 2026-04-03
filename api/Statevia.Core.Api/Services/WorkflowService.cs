using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    private static readonly JsonSerializerOptions DedupJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IWorkflowEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly ICommandDedupService _dedupService;
    private readonly IWorkflowRepository _workflows;
    private readonly IDefinitionRepository _definitions;
    private readonly ICommandDedupRepository _dedup;
    private readonly IEventStoreRepository _eventStore;
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    public WorkflowService(
        IWorkflowEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        ICommandDedupService dedupService,
        IWorkflowRepository workflows,
        IDefinitionRepository definitions,
        ICommandDedupRepository dedup,
        IEventStoreRepository eventStore,
        IDbContextFactory<CoreDbContext> dbFactory)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _dedupService = dedupService;
        _workflows = workflows;
        _definitions = definitions;
        _dedup = dedup;
        _eventStore = eventStore;
        _dbFactory = dbFactory;
    }

    public async Task<WorkflowResponse> StartAsync(
        string tenantId,
        StartWorkflowRequest request,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct)
    {
        var requestHash = ComputeStartRequestHash(request);
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, method, path, requestHash);

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

            var conflicting = await _dedup.FindValidConflictingRequestHashAsync(
                tenantId,
                key.Endpoint,
                key.IdempotencyKey,
                requestHash,
                dedupCheckTime,
                ct).ConfigureAwait(false);
            if (conflicting is not null)
            {
                throw new IdempotencyConflictException(
                    "The same X-Idempotency-Key was used with a different request body.");
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
        _engine.Start(compiled, engineId, request.Input);

        var displayId = await _displayIds.AllocateAsync("workflow", workflowId, ct).ConfigureAwait(false);
        var status = MapStatus(_engine.GetSnapshot(engineId));
        var graphJson = _engine.ExportExecutionGraph(engineId);

        var createdAt = DateTime.UtcNow;

        var workflowRow = new WorkflowRow
        {
            WorkflowId = workflowId,
            TenantId = tenantId,
            DefinitionId = defUuid.Value,
            Status = status,
            StartedAt = createdAt,
            UpdatedAt = createdAt,
            CancelRequested = false,
            RestartLost = false
        };
        var snapshotRow = new ExecutionGraphSnapshotRow
        {
            WorkflowId = workflowId,
            GraphJson = graphJson,
            UpdatedAt = createdAt
        };

        var response = new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflowId,
            Status = status,
            StartedAt = createdAt
        };

        var startedPayload = JsonSerializer.Serialize(
            new { definitionId = defUuid.Value.ToString(), tenantId },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        try
        {
            await _workflows.AddWorkflowAndSnapshotAsync(db, workflowRow, snapshotRow, ct).ConfigureAwait(false);
            await _eventStore.AppendAsync(db, workflowId, EventStoreEventType.WorkflowStarted, startedPayload, ct).ConfigureAwait(false);

            if (dedupKey is { } saveKey)
            {
                var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await _dedup.SaveAsync(db, new CommandDedupRow
                {
                    DedupKey = saveKey.DedupKey,
                    Endpoint = saveKey.Endpoint,
                    IdempotencyKey = saveKey.IdempotencyKey,
                    RequestHash = requestHash,
                    StatusCode = StatusCodes.Status201Created,
                    ResponseBody = responseJson,
                    CreatedAt = createdAt,
                    ExpiresAt = createdAt.AddHours(24)
                }, ct).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
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

    public async Task<PagedResult<WorkflowResponse>> ListPagedAsync(
        string tenantId,
        int offset,
        int limit,
        string? status,
        CancellationToken ct)
    {
        var (total, pairs) = await _workflows.ListWithDisplayIdsPageAsync(tenantId, offset, limit, status, ct).ConfigureAwait(false);
        var items = pairs.Select(p => new WorkflowResponse
        {
            DisplayId = p.DisplayId ?? p.Workflow.WorkflowId.ToString(),
            ResourceId = p.Workflow.WorkflowId,
            Status = p.Workflow.Status,
            StartedAt = p.Workflow.StartedAt,
            UpdatedAt = p.Workflow.UpdatedAt,
            CancelRequested = p.Workflow.CancelRequested,
            RestartLost = p.Workflow.RestartLost
        }).ToList();

        return new PagedResult<WorkflowResponse>
        {
            Items = items,
            TotalCount = total,
            Offset = offset,
            Limit = limit,
            HasMore = offset + items.Count < total
        };
    }

    public async Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", idOrUuid, ct).ConfigureAwait(false) ?? workflow.WorkflowId.ToString("D");

        return new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflow.WorkflowId,
            Status = workflow.Status,
            StartedAt = workflow.StartedAt,
            UpdatedAt = workflow.UpdatedAt,
            CancelRequested = workflow.CancelRequested,
            RestartLost = workflow.RestartLost
        };
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

        var (projStatus, projCancel, projGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var cancelPayload = JsonSerializer.Serialize(
            new { tenantId },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var dbCancel = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var txCancel = await dbCancel.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        try
        {
            await _workflows.UpdateWorkflowAndSnapshotAsync(dbCancel, uuid.Value, projStatus, projCancel, projGraphJson, ct).ConfigureAwait(false);
            await _eventStore.AppendAsync(dbCancel, uuid.Value, EventStoreEventType.WorkflowCancelled, cancelPayload, ct).ConfigureAwait(false);

            if (dedupKey is { } saveKey)
            {
                var now = DateTime.UtcNow;
                await _dedup.SaveAsync(dbCancel, new CommandDedupRow
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

            await dbCancel.SaveChangesAsync(ct).ConfigureAwait(false);
            await txCancel.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await txCancel.RollbackAsync(ct).ConfigureAwait(false);
            throw;
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

        var (pubStatus, pubCancel, pubGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var dbPub = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var txPub = await dbPub.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        try
        {
            await _workflows.UpdateWorkflowAndSnapshotAsync(dbPub, uuid.Value, pubStatus, pubCancel, pubGraphJson, ct).ConfigureAwait(false);
            await _eventStore.AppendAsync(dbPub, uuid.Value, EventStoreEventType.EventPublished, publishedPayload, ct).ConfigureAwait(false);

            if (dedupKey is { } saveKey)
            {
                var now = DateTime.UtcNow;
                await _dedup.SaveAsync(dbPub, new CommandDedupRow
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

            await dbPub.SaveChangesAsync(ct).ConfigureAwait(false);
            await txPub.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await txPub.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        return await BuildWorkflowViewInternalAsync(tenantId, uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    public async Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct)
    {
        if (atSeq < 1)
            throw new ArgumentException("atSeq must be >= 1");

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var maxSeq = await _eventStore.GetMaxSeqAsync(uuid.Value, ct).ConfigureAwait(false);
        if (maxSeq == 0)
            return await BuildWorkflowViewInternalAsync(tenantId, uuid.Value, idOrUuid, ct).ConfigureAwait(false);

        if (atSeq > maxSeq)
            throw new NotFoundException("atSeq out of range");

        return await BuildWorkflowViewInternalAsync(tenantId, uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    public async Task<ExecutionEventsResponseDto> ListEventsAsync(
        string tenantId,
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct)
    {
        if (afterSeq < 0)
            throw new ArgumentException("afterSeq must be >= 0");
        if (limit is < 1 or > 5000)
            throw new ArgumentException("limit must be between 1 and 5000");

        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException("Workflow not found");

        var workflow = await _workflows.GetByIdAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", idOrUuid, ct).ConfigureAwait(false) ?? workflow.WorkflowId.ToString("D");
        var graphJson = await GetGraphJsonAsync(tenantId, idOrUuid, ct).ConfigureAwait(false);
        var patchNodes = WorkflowViewMapper.MapGraphPatchNodes(graphJson);

        var (items, hasMore) = await _eventStore.ListAfterSeqAsync(uuid.Value, afterSeq, limit, ct).ConfigureAwait(false);

        var typeStarted = EventStoreEventType.WorkflowStarted.ToPersistedString();
        var typeCancelled = EventStoreEventType.WorkflowCancelled.ToPersistedString();
        var typePublished = EventStoreEventType.EventPublished.ToPersistedString();

        var events = new List<TimelineEventDto>(items.Count);
        foreach (var row in items)
        {
            var at = row.OccurredAt.ToString("O");
            var timelineEvent = row.Type switch
            {
                _ when row.Type == typeStarted => new TimelineEventDto
                {
                    Seq = row.Seq,
                    Type = "ExecutionStatusChanged",
                    ExecutionId = displayId,
                    To = "Running",
                    At = at
                },
                _ when row.Type == typeCancelled => new TimelineEventDto
                {
                    Seq = row.Seq,
                    Type = "ExecutionStatusChanged",
                    ExecutionId = displayId,
                    To = "Cancelled",
                    At = at
                },
                _ when row.Type == typePublished => new TimelineEventDto
                {
                    Seq = row.Seq,
                    Type = "GraphUpdated",
                    ExecutionId = displayId,
                    Patch = new GraphUpdatedPatchDto { Nodes = patchNodes },
                    At = at
                },
                _ => null
            };
            if (timelineEvent is not null)
                events.Add(timelineEvent);
        }

        return new ExecutionEventsResponseDto { Events = events, HasMore = hasMore };
    }

    public Task ResumeNodeAsync(
        string tenantId,
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (string.IsNullOrWhiteSpace(resumeKey))
            throw new ArgumentException("resumeKey is required");

        return PublishEventAsync(tenantId, idOrUuid, resumeKey!, idempotencyKey, method, path, ct);
    }

    private async Task<WorkflowViewDto> BuildWorkflowViewInternalAsync(string tenantId, Guid uuid, string idOrUuidForDisplay, CancellationToken ct)
    {
        var workflow = await _workflows.GetByIdAsync(tenantId, uuid, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");

        var snapshot = await _workflows.GetSnapshotByWorkflowIdAsync(uuid, ct).ConfigureAwait(false);
        if (snapshot is null)
            throw new NotFoundException("Workflow not found");

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", idOrUuidForDisplay, ct).ConfigureAwait(false) ?? workflow.WorkflowId.ToString("D");
        var graphId = await _displayIds.GetDisplayIdAsync("definition", workflow.DefinitionId.ToString("D"), ct).ConfigureAwait(false) ?? workflow.DefinitionId.ToString("D");
        return WorkflowViewMapper.BuildWorkflowView(workflow, snapshot.GraphJson, displayId, graphId);
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

    private static string ComputeStartRequestHash(StartWorkflowRequest request)
    {
        var normalized = JsonSerializer.Serialize(
            new
            {
                definitionId = request.DefinitionId,
                input = request.Input
            },
            DedupJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
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
        var (status, cancelRequested, graphJson) = BuildProjectionFromEngine(workflowId);
        await _workflows.UpdateWorkflowAndSnapshotAsync(
            workflowId,
            status,
            cancelRequested,
            graphJson,
            ct).ConfigureAwait(false);
    }

    private (string Status, bool? CancelRequested, string GraphJson) BuildProjectionFromEngine(Guid workflowId)
    {
        var engineId = workflowId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot?.IsCancelled, graphJson);
    }
}

