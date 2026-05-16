using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

internal sealed class WorkflowService : IWorkflowService
{
    /// <summary>
    /// イベント本文・冪等キャッシュ応答・リクエストハッシュ入力など、camelCase でシリアル化する際の共有オプション（都度 new しない）。
    /// </summary>
    private static readonly JsonSerializerOptions CamelCaseJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// <see cref="CommandDedupRow.ResponseBody"/> からの逆シリアル用（プロパティ名の大文字小文字を許容）。
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    /// <summary>
    /// Serializable 永続化の再試行ループにおける試行番号・上限・累積バックオフ（待機間隔・予算計算に用いる）。
    /// </summary>
    private readonly record struct SerializablePersistenceRetryProgress(
        int Attempt,
        int MaxAttempts,
        int TotalBackoffMs);

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
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var requestHash = ComputeStartRequestHash(request);
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, requestContext.Method, requestContext.Path, requestHash);

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
        var graphId = await _displayIds.GetDisplayIdAsync("definition", defUuid.Value.ToString("D"), ct).ConfigureAwait(false)
            ?? defUuid.Value.ToString("D");
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
            GraphId = graphId,
            Status = status,
            StartedAt = createdAt
        };

        var startedPayload = JsonSerializer.Serialize(
            new { definitionId = defUuid.Value.ToString(), tenantId },
            CamelCaseJsonSerializerOptions);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        try
        {
            await _workflows.AddWorkflowAndSnapshotAsync(db, workflowRow, snapshotRow, ct).ConfigureAwait(false);
            await _eventStore.AppendAsync(db, workflowId, EventStoreEventType.WorkflowStarted, startedPayload, ct).ConfigureAwait(false);

            if (dedupKey is { } saveKey)
            {
                var responseJson = JsonSerializer.Serialize(response, CamelCaseJsonSerializerOptions);
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
            GraphId = p.Workflow.DefinitionId.ToString("D"),
            Status = p.Workflow.Status,
            StartedAt = p.Workflow.StartedAt,
            UpdatedAt = p.Workflow.UpdatedAt,
            CancelRequested = p.Workflow.CancelRequested,
            RestartLost = p.Workflow.RestartLost
        }).ToList();
    }

    public async Task<PagedResult<WorkflowResponse>> ListPagedAsync(
        string tenantId,
        WorkflowListQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var offset = query.Offset;
        var limit = query.Limit ?? throw new ArgumentException("limit is required for paged list");
        var status = query.Status;
        var definitionId = query.DefinitionId;
        var nameContains = query.Name;
        var sortBy = query.SortBy;
        var sortOrder = query.SortOrder;

        Guid? definitionIdFilter = null;
        if (!string.IsNullOrWhiteSpace(definitionId))
        {
            var defUuid = await _displayIds.ResolveAsync("definition", definitionId!.Trim(), ct).ConfigureAwait(false);
            if (defUuid is null)
            {
                return new PagedResult<WorkflowResponse>
                {
                    Items = new List<WorkflowResponse>(),
                    TotalCount = 0,
                    Offset = offset,
                    Limit = limit,
                    HasMore = false
                };
            }
            definitionIdFilter = defUuid;
        }

        var nameFilter = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
        var pageQuery = new WorkflowListPageQuery(
            Page: new PageQuery(offset, limit),
            Sort: new SortQuery(sortBy, sortOrder),
            StatusFilter: status,
            DefinitionIdFilter: definitionIdFilter,
            NameContains: nameFilter);
        var (total, pairs) = await _workflows
            .ListWithDisplayIdsPageAsync(tenantId, pageQuery, ct)
            .ConfigureAwait(false);
        var items = pairs.Select(p => new WorkflowResponse
        {
            DisplayId = p.DisplayId ?? p.Workflow.WorkflowId.ToString(),
            ResourceId = p.Workflow.WorkflowId,
            GraphId = p.Workflow.DefinitionId.ToString("D"),
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
        var graphId = await _displayIds.GetDisplayIdAsync("definition", workflow.DefinitionId.ToString("D"), ct).ConfigureAwait(false)
            ?? workflow.DefinitionId.ToString("D");

        return new WorkflowResponse
        {
            DisplayId = displayId,
            ResourceId = workflow.WorkflowId,
            GraphId = graphId,
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
        await EnsureWorkflowExistsAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        var row = await _workflows.GetSnapshotByWorkflowIdAsync(uuid.Value, ct).ConfigureAwait(false);
        return row is null ? throw new NotFoundException("Workflow not found") : row.GraphJson;
    }

    public async Task EnsureWorkflowExistsAsync(string tenantId, Guid workflowId, CancellationToken ct)
    {
        var workflow = await _workflows.GetByIdAsync(tenantId, workflowId, ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException("Workflow not found");
    }

    public async Task<string?> TryGetSnapshotGraphJsonByWorkflowIdAsync(Guid workflowId, CancellationToken ct)
    {
        var row = await _workflows.GetSnapshotByWorkflowIdAsync(workflowId, ct).ConfigureAwait(false);
        return row?.GraphJson;
    }

    public async Task CancelAsync(
        string tenantId,
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, requestContext.Method, requestContext.Path);

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
            CamelCaseJsonSerializerOptions);

        await CommitWorkflowMutationSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (dbCancel, ctInner) =>
            {
                await _workflows.UpdateWorkflowAndSnapshotAsync(dbCancel, uuid.Value, projStatus, projCancel, projGraphJson, ctInner).ConfigureAwait(false);
                if (!skipCancelEventAppend)
                {
                    await _eventStore.AppendAsync(dbCancel, uuid.Value, EventStoreEventType.WorkflowCancelled, cancelPayload, ctInner).ConfigureAwait(false);
                }
                else
                {
                    await _eventStore.TryAppendIfAbsentByClientEventAsync(
                        dbCancel,
                        uuid.Value,
                        clientEventId,
                        EventStoreEventType.WorkflowCancelled,
                        cancelPayload,
                        ctInner).ConfigureAwait(false);
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
                    }, ctInner).ConfigureAwait(false);
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
                    ctInner).ConfigureAwait(false);

                await dbCancel.SaveChangesAsync(ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    public async Task PublishEventAsync(
        string tenantId,
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, requestContext.Method, requestContext.Path);

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
            CamelCaseJsonSerializerOptions);

        await CommitWorkflowMutationSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (dbPub, ctInner) =>
            {
                await _workflows.UpdateWorkflowAndSnapshotAsync(dbPub, uuid.Value, pubStatus, pubCancel, pubGraphJson, ctInner).ConfigureAwait(false);
                if (!skipEventAppend)
                {
                    await _eventStore.AppendAsync(dbPub, uuid.Value, EventStoreEventType.EventPublished, publishedPayload, ctInner).ConfigureAwait(false);
                }
                else
                {
                    await _eventStore.TryAppendIfAbsentByClientEventAsync(
                        dbPub,
                        uuid.Value,
                        clientEventId,
                        EventStoreEventType.EventPublished,
                        publishedPayload,
                        ctInner).ConfigureAwait(false);
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
                    }, ctInner).ConfigureAwait(false);
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
                    ctInner).ConfigureAwait(false);

                await dbPub.SaveChangesAsync(ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
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
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (string.IsNullOrWhiteSpace(resumeKey))
            throw new ArgumentException("resumeKey is required");

        return PublishEventAsync(tenantId, idOrUuid, resumeKey!, idempotencyKey, requestContext, ct);
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

    private static WorkflowResponse? DeserializeCachedWorkflowResponse(CommandDedupRow existing)
    {
        if (string.IsNullOrEmpty(existing.ResponseBody))
            return null;
        return JsonDeserialize.TryDeserialize<WorkflowResponse>(existing.ResponseBody, CaseInsensitiveJsonSerializerOptions, out var deserialized)
            ? deserialized
            : null;
    }

    private static string ComputeStartRequestHash(StartWorkflowRequest request)
    {
        var normalized = JsonSerializer.Serialize(
            new
            {
                definitionId = request.DefinitionId,
                input = request.Input
            },
            CamelCaseJsonSerializerOptions);
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
            _logger.SkipProjectionQueueUpdateDebug(workflowId);
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
        switch (decision)
        {
            case EventDeliveryLogDecisions.Failed:
            case EventDeliveryLogDecisions.BackoffBudgetExhausted:
                _logger.EventDeliveryDecisionError(
                    exception!,
                    traceId,
                    workflowId,
                    tenantId,
                    clientEventId,
                    decision,
                    attempt,
                    elapsedMs,
                    errorCode);
                break;
            case EventDeliveryLogDecisions.Retry:
            case EventDeliveryLogDecisions.AbortedTimeout:
                _logger.EventDeliveryDecisionWarning(
                    exception!,
                    traceId,
                    workflowId,
                    tenantId,
                    clientEventId,
                    decision,
                    attempt,
                    elapsedMs,
                    errorCode);
                break;
            default:
                _logger.EventDeliveryDecisionInformation(
                    traceId,
                    workflowId,
                    tenantId,
                    clientEventId,
                    decision,
                    attempt,
                    elapsedMs,
                    errorCode);
                break;
        }
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

    private static async Task TryRollbackSerializableTransactionAsync(
        IDbContextTransaction tx,
        CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // 破損済みトランザクションのロールバックで起きうる競合は無視する。
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// イベント配送・キャンセル等の Serializable 永続化を試み、PostgreSQL の直列化失敗（40001）や
    /// デッドロック（40P01）のときは <see cref="EventDeliveryRetryOptions"/> に従いバックオフして再試行する。
    /// Engine への適用は呼び出し側で一度だけ行い、本メソッドは投影の DB 書き込みのみを繰り返す。
    /// </summary>
    private async Task CommitWorkflowMutationSerializableWithRetryAsync(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        Func<CoreDbContext, CancellationToken, Task> applyAndSaveAsync,
        CancellationToken ct)
    {
        var retryOptions = _eventDeliveryRetryOptions.Value;
        var maxAttempts = Math.Max(1, retryOptions.SerializablePersistenceMaxAttempts);
        var totalBackoffMs = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

            async Task PersistFailureAsync(Exception failure)
            {
                await TryRollbackSerializableTransactionAsync(tx, ct).ConfigureAwait(false);
                totalBackoffMs = await ApplySerializablePersistenceFailureAsync(
                    failure,
                    tenantId,
                    workflowId,
                    clientEventId,
                    new SerializablePersistenceRetryProgress(attempt, maxAttempts, totalBackoffMs),
                    retryOptions,
                    ct).ConfigureAwait(false);
            }

            try
            {
                await applyAndSaveAsync(db, ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (EventDeliveryRetryPolicy.IsNonRetryableTimeoutOrCancellation(ex))
            {
                await TryRollbackSerializableTransactionAsync(tx, ct).ConfigureAwait(false);
                await TryMarkEventDeliveryFailedAsync(tenantId, workflowId, clientEventId, ct).ConfigureAwait(false);
                throw;
            }
            catch (DbUpdateException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Serializable persistence retry loop ended unexpectedly.");
    }

    /// <summary>
    /// Serializable 永続化が競合・その他の失敗で終わったあとの処理。
    /// 再試行可能な競合ならバックオフ後に累積待機時間を返し、呼び出し側はループを続行する。
    /// それ以外は <see cref="TryMarkEventDeliveryFailedAsync"/> の後に元例外を再送出する。
    /// </summary>
    /// <returns>バックオフ適用後の累積待機時間（ミリ秒）。</returns>
    /// <exception cref="InvalidOperationException">バックオフ予算を使い切った場合。</exception>
    private async Task<int> ApplySerializablePersistenceFailureAsync(
        Exception ex,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        SerializablePersistenceRetryProgress retryProgress,
        EventDeliveryRetryOptions retryOptions,
        CancellationToken ct)
    {
        var conflict = EventDeliveryRetryPolicy.IsPostgresSerializableOrDeadlockConflict(ex);
        if (conflict && retryProgress.Attempt < retryProgress.MaxAttempts)
        {
            var failureIndex = retryProgress.Attempt - 1;
            var delayMs = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(failureIndex, retryOptions, Random.Shared);
            if (retryOptions.MaxTotalBackoffMs > 0)
            {
                var remainingBudgetMs = retryOptions.MaxTotalBackoffMs - retryProgress.TotalBackoffMs;
                if (remainingBudgetMs <= 0)
                {
                    LogSerializablePersistRetry(
                        tenantId,
                        workflowId,
                        clientEventId,
                        retryProgress.Attempt,
                        retryProgress.MaxAttempts,
                        delayMs: 0,
                        ex.Message);
                    await TryMarkEventDeliveryFailedAsync(tenantId, workflowId, clientEventId, ct).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        "Serializable persistence retry stopped: total backoff budget exhausted.",
                        ex);
                }

                delayMs = Math.Min(delayMs, remainingBudgetMs);
            }

            var newTotalBackoffMs = retryProgress.TotalBackoffMs + delayMs;
            LogSerializablePersistRetry(
                tenantId,
                workflowId,
                clientEventId,
                retryProgress.Attempt,
                retryProgress.MaxAttempts,
                delayMs,
                ex.Message);
            if (delayMs > 0)
                await Task.Delay(delayMs, ct).ConfigureAwait(false);

            return newTotalBackoffMs;
        }

        if (conflict)
        {
            LogSerializablePersistRetry(
                tenantId,
                workflowId,
                clientEventId,
                retryProgress.Attempt,
                retryProgress.MaxAttempts,
                delayMs: 0,
                ex.Message);
        }

        await TryMarkEventDeliveryFailedAsync(tenantId, workflowId, clientEventId, ct).ConfigureAwait(false);
        ExceptionDispatchInfo.Capture(ex).Throw();
        return 0;
    }

    private void LogSerializablePersistRetry(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        int attempt,
        int maxAttempts,
        int delayMs,
        string failureMessage)
    {
        var traceId = GetTraceIdOrEmpty();
        _logger.SerializablePersistRetry(
            traceId,
            workflowId,
            tenantId,
            clientEventId,
            attempt,
            maxAttempts,
            delayMs,
            failureMessage);
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

