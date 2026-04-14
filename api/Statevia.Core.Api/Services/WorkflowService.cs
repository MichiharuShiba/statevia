using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Services;

public sealed class WorkflowService : IWorkflowService
{
    private static readonly JsonSerializerOptions DedupJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary><c>event_delivery_decision</c> 構造化ログの <c>decision</c> プロパティ値。</summary>
    private static class EventDeliveryLogDecisions
    {
        internal const string Inserted = "inserted";
        internal const string DuplicateKey = "duplicate_key";
        internal const string AbortedTimeout = "aborted_timeout";
        internal const string Retry = "retry";
        internal const string BackoffBudgetExhausted = "backoff_budget_exhausted";
        internal const string Failed = "failed";
    }

    /// <summary>
    /// <c>event_delivery_decision</c> 構造化ログの <c>errorCode</c>、および dedup 行の既知 <c>error_code</c> 値。
    /// </summary>
    private static class EventDeliveryLogErrorCodes
    {
        internal const string None = "none";
        internal const string UniqueViolation = "unique_violation";
        internal const string PersistFailed = "persist_failed";
    }

    private readonly IWorkflowEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly ICommandDedupService _dedupService;
    private readonly IWorkflowRepository _workflows;
    private readonly IDefinitionRepository _definitions;
    private readonly ICommandDedupRepository _dedup;
    private readonly IEventStoreRepository _eventStore;
    private readonly IEventDeliveryDedupRepository _eventDeliveryDedup;
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly ILogger<WorkflowService> _logger;
    private readonly IOptions<EventDeliveryRetryOptions> _eventDeliveryRetryOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkflowProjectionUpdateQueue _projectionUpdateQueue;

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
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IDbContextFactory<CoreDbContext> dbFactory,
        ILogger<WorkflowService> logger,
        IOptions<EventDeliveryRetryOptions> eventDeliveryRetryOptions,
        IHttpContextAccessor httpContextAccessor,
        IWorkflowProjectionUpdateQueue? projectionUpdateQueue = null)
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
        _eventDeliveryDedup = eventDeliveryDedup;
        _dbFactory = dbFactory;
        _logger = logger;
        _eventDeliveryRetryOptions = eventDeliveryRetryOptions;
        _httpContextAccessor = httpContextAccessor;
        _projectionUpdateQueue = projectionUpdateQueue ?? NoopWorkflowProjectionUpdateQueue.Instance;
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

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(tenantId, uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        EnsureEngineRuntimePresentForMutation(uuid.Value, workflow);

        var cancelApply = await _engine.CancelAsync(uuid.Value.ToString(), clientEventId).ConfigureAwait(false);
        var skipCancelEventAppend = cancelApply.IsAlreadyApplied;

        var (projStatus, projCancel, projGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var cancelPayload = JsonSerializer.Serialize(
            new { tenantId },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var dbCancel = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var txCancel = await dbCancel.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        try
        {
            await _workflows.UpdateWorkflowAndSnapshotAsync(dbCancel, uuid.Value, projStatus, projCancel, projGraphJson, ct).ConfigureAwait(false);
            if (!skipCancelEventAppend)
            {
                await _eventStore.AppendAsync(dbCancel, uuid.Value, EventStoreEventType.WorkflowCancelled, cancelPayload, ct).ConfigureAwait(false);
            }
            else
            {
                await _eventStore.TryAppendIfAbsentByClientEventAsync(
                    dbCancel,
                    uuid.Value,
                    clientEventId,
                    EventStoreEventType.WorkflowCancelled,
                    cancelPayload,
                    ct).ConfigureAwait(false);
            }

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

            var nowUtc = DateTime.UtcNow;
            await _eventDeliveryDedup.TryUpdateStatusAsync(
                dbCancel,
                tenantId,
                uuid.Value,
                clientEventId,
                EventDeliveryDedupStatuses.Applied,
                nowUtc,
                appliedAt: nowUtc,
                errorCode: null,
                ct).ConfigureAwait(false);

            await dbCancel.SaveChangesAsync(ct).ConfigureAwait(false);
            await txCancel.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await txCancel.RollbackAsync(ct).ConfigureAwait(false);
            await TryMarkEventDeliveryFailedAsync(tenantId, uuid.Value, clientEventId, ct).ConfigureAwait(false);
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

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(tenantId, uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        EnsureEngineRuntimePresentForMutation(uuid.Value, workflow);

        var publishApply = _engine.PublishEvent(uuid.Value.ToString(), eventName, clientEventId);
        var skipEventAppend = publishApply.IsAlreadyApplied;

        var (pubStatus, pubCancel, pubGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await using var dbPub = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var txPub = await dbPub.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        try
        {
            await _workflows.UpdateWorkflowAndSnapshotAsync(dbPub, uuid.Value, pubStatus, pubCancel, pubGraphJson, ct).ConfigureAwait(false);
            if (!skipEventAppend)
            {
                await _eventStore.AppendAsync(dbPub, uuid.Value, EventStoreEventType.EventPublished, publishedPayload, ct).ConfigureAwait(false);
            }
            else
            {
                await _eventStore.TryAppendIfAbsentByClientEventAsync(
                    dbPub,
                    uuid.Value,
                    clientEventId,
                    EventStoreEventType.EventPublished,
                    publishedPayload,
                    ct).ConfigureAwait(false);
            }

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

            var nowUtc = DateTime.UtcNow;
            await _eventDeliveryDedup.TryUpdateStatusAsync(
                dbPub,
                tenantId,
                uuid.Value,
                clientEventId,
                EventDeliveryDedupStatuses.Applied,
                nowUtc,
                appliedAt: nowUtc,
                errorCode: null,
                ct).ConfigureAwait(false);

            await dbPub.SaveChangesAsync(ct).ConfigureAwait(false);
            await txPub.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await txPub.RollbackAsync(ct).ConfigureAwait(false);
            await TryMarkEventDeliveryFailedAsync(tenantId, uuid.Value, clientEventId, ct).ConfigureAwait(false);
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

    /// <summary>
    /// 投影が終了状態かどうかを、<c>workflows.status</c> の文字列値から判定する。
    /// </summary>
    private static bool IsTerminalWorkflowProjectionStatus(string status) =>
        status is "Completed" or "Cancelled" or "Failed";

    /// <summary>
    /// キャンセル／イベント発行の適用直前に、当該ワークフローがこのプロセスのエンジンへ読み込まれていることを検証する。
    /// API 再起動などでインメモリ実行が失われた場合、DB 投影を壊さないよう <see cref="ArgumentException"/> を投げる（HTTP 422）。
    /// </summary>
    private void EnsureEngineRuntimePresentForMutation(Guid workflowId, WorkflowRow workflow)
    {
        if (_engine.GetSnapshot(workflowId.ToString()) is not null)
            return;

        if (IsTerminalWorkflowProjectionStatus(workflow.Status))
        {
            throw new ArgumentException(
                "The workflow is already in a terminal state in the database projection, but there is no in-memory instance in this API process. Cancel or event delivery cannot be applied.",
                paramName: null);
        }

        throw new ArgumentException(
            "The workflow execution state is not loaded in this API process (for example after a restart). Commands cannot be applied while the in-memory runtime is missing.",
            paramName: null);
    }

    public async Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct)
    {
        var (status, cancelRequested, graphJson) = BuildProjectionFromEngineForQueue(workflowId);
        if (status is null || graphJson is null)
            return;

        await _workflows.UpdateWorkflowAndSnapshotAsync(
            workflowId,
            status,
            cancelRequested,
            graphJson,
            ct).ConfigureAwait(false);
    }

    private Task UpdateProjectionAsync(Guid workflowId, CancellationToken ct) =>
        UpdateProjectionFromEngineAsync(workflowId, ct);

    private (string Status, bool? CancelRequested, string GraphJson) BuildProjectionFromEngine(Guid workflowId)
    {
        var engineId = workflowId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot?.IsCancelled, graphJson);
    }

    private (string? status, bool? cancelRequested, string? graphJson) BuildProjectionFromEngineForQueue(Guid workflowId)
    {
        var engineId = workflowId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        if (snapshot is null)
        {
            _logger.LogDebug("Skip projection queue update because runtime is missing for workflow {WorkflowId}", workflowId);
            return (null, null, null);
        }

        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot.IsCancelled, graphJson);
    }

    private sealed class NoopWorkflowProjectionUpdateQueue : IWorkflowProjectionUpdateQueue
    {
        internal static readonly NoopWorkflowProjectionUpdateQueue Instance = new();

        public Task EnqueueAsync(Guid workflowId, CancellationToken ct) => Task.CompletedTask;

        public Task DrainAsync(Guid workflowId, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 現在リクエストの相関 ID（無ければ空文字）を返す。
    /// </summary>
    private string GetTraceIdOrEmpty()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(RequestLogContext.TraceIdItemKey, out var traceObject) is true
            && traceObject is string traceId)
            return traceId;

        return string.Empty;
    }

    /// <summary>
    /// イベント配送 dedup の観測性用に必須キーを構造化ログへ書き出す。
    /// </summary>
    private void LogEventDeliveryDecision(
        string traceId,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode,
        Exception? exception = null)
    {
        var level = decision switch
        {
            EventDeliveryLogDecisions.Failed or EventDeliveryLogDecisions.BackoffBudgetExhausted => LogLevel.Error,
            EventDeliveryLogDecisions.Retry or EventDeliveryLogDecisions.AbortedTimeout => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(
            level,
            exception,
            "event_delivery_decision traceId={traceId} workflowId={workflowId} tenantId={tenantId} clientEventId={clientEventId} decision={decision} attempt={attempt} elapsedMs={elapsedMs} errorCode={errorCode}",
            traceId,
            workflowId,
            tenantId,
            clientEventId,
            decision,
            attempt,
            elapsedMs,
            errorCode);
    }

    /// <summary>
    /// ログ用のエラー分類コード（例外種別・SQLSTATE 等の短い識別子）。
    /// </summary>
    private static string MapErrorCode(Exception exception) =>
        exception switch
        {
            TaskCanceledException => nameof(TaskCanceledException),
            OperationCanceledException => nameof(OperationCanceledException),
            TimeoutException => nameof(TimeoutException),
            DbUpdateException dbUpdateException =>
                dbUpdateException.InnerException?.GetType().Name ?? nameof(DbUpdateException),
            _ => exception.GetType().Name
        };

    /// <summary>
    /// <c>event_delivery_dedup</c> に RECEIVED を先行挿入する。一意制約違反時は既存行を読み、
    /// 既に APPLIED なら true（呼び出し側は即 return）。それ以外は false（処理継続）。
    /// DB 一時障害時は設定に従い段階的バックオフで再試行する（タイムアウト系は再試行しない）。
    /// </summary>
    private async Task<bool> TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        var retryOptions = _eventDeliveryRetryOptions.Value;
        var traceId = GetTraceIdOrEmpty();
        var acceptedAt = DateTime.UtcNow;
        var row = new EventDeliveryDedupRow
        {
            TenantId = tenantId,
            WorkflowId = workflowId,
            ClientEventId = clientEventId,
            BatchId = null,
            Status = EventDeliveryDedupStatuses.Received,
            AcceptedAt = acceptedAt,
            AppliedAt = null,
            ErrorCode = null,
            UpdatedAt = acceptedAt
        };

        var totalBackoffMs = 0;

        for (var attempt = 1; attempt <= retryOptions.MaxAttempts; attempt++)
        {
            var attemptStopwatch = Stopwatch.StartNew();
            try
            {
                await _eventDeliveryDedup.InsertReceivedAsync(row, cancellationToken).ConfigureAwait(false);
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    traceId,
                    tenantId,
                    workflowId,
                    clientEventId,
                    decision: EventDeliveryLogDecisions.Inserted,
                    attempt,
                    attemptStopwatch.ElapsedMilliseconds,
                    errorCode: EventDeliveryLogErrorCodes.None);
                return false;
            }
            catch (DbUpdateException dbUpdateException)
                when (EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdateException))
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    traceId,
                    tenantId,
                    workflowId,
                    clientEventId,
                    decision: EventDeliveryLogDecisions.DuplicateKey,
                    attempt,
                    attemptStopwatch.ElapsedMilliseconds,
                    errorCode: EventDeliveryLogErrorCodes.UniqueViolation);

                var existing = await _eventDeliveryDedup
                    .FindAsync(tenantId, workflowId, clientEventId, cancellationToken)
                    .ConfigureAwait(false);
                if (existing is { Status: EventDeliveryDedupStatuses.Applied })
                    return true;

                if (existing is null)
                    throw;

                return false;
            }
            catch (Exception exception)
                when (EventDeliveryRetryPolicy.IsNonRetryableTimeoutOrCancellation(exception))
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    traceId,
                    tenantId,
                    workflowId,
                    clientEventId,
                    decision: EventDeliveryLogDecisions.AbortedTimeout,
                    attempt,
                    attemptStopwatch.ElapsedMilliseconds,
                    MapErrorCode(exception),
                    exception);
                throw;
            }
            catch (Exception exception) when (
                EventDeliveryRetryPolicy.IsTransientInfrastructureFailure(exception)
                && attempt < retryOptions.MaxAttempts)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    traceId,
                    tenantId,
                    workflowId,
                    clientEventId,
                    decision: EventDeliveryLogDecisions.Retry,
                    attempt,
                    attemptStopwatch.ElapsedMilliseconds,
                    MapErrorCode(exception),
                    exception);

                var failureIndex = attempt - 1;
                var delayMs = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(
                    failureIndex,
                    retryOptions,
                    Random.Shared);

                if (retryOptions.MaxTotalBackoffMs > 0)
                {
                    var remainingBudgetMs = retryOptions.MaxTotalBackoffMs - totalBackoffMs;
                    if (remainingBudgetMs <= 0)
                    {
                        attemptStopwatch.Stop();
                        LogEventDeliveryDecision(
                            traceId,
                            tenantId,
                            workflowId,
                            clientEventId,
                            decision: EventDeliveryLogDecisions.BackoffBudgetExhausted,
                            attempt,
                            attemptStopwatch.ElapsedMilliseconds,
                            errorCode: EventDeliveryLogDecisions.BackoffBudgetExhausted,
                            exception);
                        throw new InvalidOperationException(
                            "Event delivery insert retry stopped: total backoff budget exhausted.",
                            exception);
                    }

                    delayMs = Math.Min(delayMs, remainingBudgetMs);
                }

                totalBackoffMs += delayMs;
                if (delayMs > 0)
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

                continue;
            }
            catch (Exception exception)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    traceId,
                    tenantId,
                    workflowId,
                    clientEventId,
                    decision: EventDeliveryLogDecisions.Failed,
                    attempt,
                    attemptStopwatch.ElapsedMilliseconds,
                    MapErrorCode(exception),
                    exception);
                throw;
            }
        }

        throw new InvalidOperationException("Event delivery insert retry loop ended unexpectedly.");
    }

    /// <summary>
    /// メイン永続化が失敗したとき、配送行を FAILED に更新する（ベストエフォート）。
    /// </summary>
    private async Task TryMarkEventDeliveryFailedAsync(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var nowUtc = DateTime.UtcNow;
            await _eventDeliveryDedup.TryUpdateStatusAsync(
                db,
                tenantId,
                workflowId,
                clientEventId,
                EventDeliveryDedupStatuses.Failed,
                nowUtc,
                appliedAt: null,
                errorCode: EventDeliveryLogErrorCodes.PersistFailed,
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types — 補助更新の失敗は本例外に影響させない
        catch
        {
            // 意図的に飲み込む
        }
#pragma warning restore CA1031
    }
}

