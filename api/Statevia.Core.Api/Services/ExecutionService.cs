using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Services;

internal sealed class ExecutionService : IExecutionService
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
    }

    private readonly IWorkflowEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly ICommandDedupService _dedupService;
    private readonly IExecutionRepository _workflows;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ICommandDedupRepository _dedup;
    private readonly IEventStoreRepository _eventStore;
    private readonly IEventDeliveryDedupRepository _eventDeliveryDedup;
    private readonly IDisplayIdWriteService _displayIdWrites;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IExecutionMutationPersistence _mutationPersistence;
    private readonly ILogger<ExecutionService> _logger;
    private readonly IOptions<EventDeliveryRetryOptions> _eventDeliveryRetryOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IExecutionProjectionUpdateQueue _projectionUpdateQueue;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "ASP.NET Core DI による明示的コンストラクタ注入。")]
    public ExecutionService(
        IWorkflowEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        ICommandDedupService dedupService,
        IExecutionRepository workflows,
        IDefinitionRepository definitions,
        IProjectAuthorizationService projectAuth,
        ITenantContextAccessor tenantContext,
        ICommandDedupRepository dedup,
        IEventStoreRepository eventStore,
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IDisplayIdWriteService displayIdWrites,
        ICoreTransactionExecutor executor,
        IExecutionMutationPersistence mutationPersistence,
        ILogger<ExecutionService> logger,
        IOptions<EventDeliveryRetryOptions> eventDeliveryRetryOptions,
        IHttpContextAccessor httpContextAccessor,
        IExecutionProjectionUpdateQueue? projectionUpdateQueue = null)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _dedupService = dedupService;
        _workflows = workflows;
        _definitions = definitions;
        _projectAuth = projectAuth;
        _tenantContext = tenantContext;
        _dedup = dedup;
        _eventStore = eventStore;
        _eventDeliveryDedup = eventDeliveryDedup;
        _displayIdWrites = displayIdWrites;
        _executor = executor;
        _mutationPersistence = mutationPersistence;
        _logger = logger;
        _eventDeliveryRetryOptions = eventDeliveryRetryOptions;
        _httpContextAccessor = httpContextAccessor;
        _projectionUpdateQueue = projectionUpdateQueue ?? NoopWorkflowProjectionUpdateQueue.Instance;
    }

    public async Task<ExecutionResponse> StartAsync(
        string tenantId,
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var requestHash = ComputeStartRequestHash(request);
        var dedupKey = _dedupService.Create(tenantId, idempotencyKey, requestContext.Method, requestContext.Path, requestHash);

        var cachedStart = await TryGetIdempotentStartResponseAsync(tenantId, dedupKey, requestHash, ct).ConfigureAwait(false);
        if (cachedStart is not null)
        {
            return cachedStart;
        }

        var defUuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, request.DefinitionId!, ct).ConfigureAwait(false);
        if (defUuid is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        var tenantInternalId = _tenantContext.GetRequiredTenantInternalId();
        var versionRow = await ResolveStartDefinitionVersionAsync(tenantInternalId, defUuid.Value, request, ct).ConfigureAwait(false);
        if (versionRow is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        await EnsureCanExecuteOnDefinitionAsync(tenantInternalId, defUuid.Value, ct).ConfigureAwait(false);

        var compiled = RestoreCompiledDefinitionFromVersion(versionRow);

        var workflowId = _idGenerator.NewGuid();
        var engineId = workflowId.ToString();
        _engine.Start(compiled, engineId, request.Input);

        var graphId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, defUuid.Value.ToString("D"), ct).ConfigureAwait(false)
            ?? defUuid.Value.ToString("D");
        var status = MapStatus(_engine.GetSnapshot(engineId));
        var graphJson = _engine.ExportExecutionGraph(engineId);

        var startedPayload = JsonSerializer.Serialize(
            new
            {
                definitionId = defUuid.Value.ToString(),
                definitionVersionId = versionRow.DefinitionVersionId.ToString(),
                definitionVersion = versionRow.Version,
                tenantId
            },
            CamelCaseJsonSerializerOptions);

        return await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var createdAt = DateTime.UtcNow;
                var displayId = await _displayIdWrites
                    .AllocateAsync(uow, DisplayIdResourceTypes.Execution, workflowId, innerCt)
                    .ConfigureAwait(false);

                var workflowRow = new ExecutionRow
                {
                    ExecutionId = workflowId,
                    TenantId = tenantId,
                    DefinitionId = defUuid.Value,
                    DefinitionVersionId = versionRow.DefinitionVersionId,
                    Status = status,
                    StartedAt = createdAt,
                    UpdatedAt = createdAt,
                    CancelRequested = false,
                    RestartLost = false
                };
                var snapshotRow = new ExecutionGraphSnapshotRow
                {
                    ExecutionId = workflowId,
                    GraphJson = graphJson,
                    UpdatedAt = createdAt
                };

                var response = new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = workflowId,
                    GraphId = graphId,
                    Status = status,
                    StartedAt = createdAt
                };

                await _workflows.AddExecutionAndSnapshotAsync(uow, workflowRow, snapshotRow, innerCt).ConfigureAwait(false);
                await _eventStore
                    .AppendAsync(uow, workflowId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                    .ConfigureAwait(false);

                if (dedupKey is { } saveKey)
                {
                    var responseJson = JsonSerializer.Serialize(response, CamelCaseJsonSerializerOptions);
                    await _dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = requestHash,
                            StatusCode = StatusCodes.Status201Created,
                            ResponseBody = responseJson,
                            CreatedAt = createdAt,
                            ExpiresAt = createdAt.AddHours(24)
                        },
                        innerCt).ConfigureAwait(false);
                }

                return response;
            },
            ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        string tenantId,
        ExecutionListQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var offset = query.Offset ?? 0;
        var limit = query.Limit ?? throw new ArgumentException("limit is required for paged list");
        var status = query.Status;
        var definitionId = query.DefinitionId;
        var nameContains = query.Name;
        var sortBy = query.SortBy;
        var sortOrder = query.SortOrder;

        Guid? definitionIdFilter = null;
        if (!string.IsNullOrWhiteSpace(definitionId))
        {
            var defUuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, definitionId!.Trim(), ct).ConfigureAwait(false);
            if (defUuid is null)
            {
                return new PagedResult<ExecutionResponse>
                {
                    Items = [],
                    TotalCount = 0,
                    Offset = offset,
                    Limit = limit,
                    HasMore = false
                };
            }
            definitionIdFilter = defUuid;
        }

        var nameFilter = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
        var pageQuery = new ExecutionListPageQuery(
            Page: new PageQuery(offset, limit),
            Sort: new SortQuery(sortBy, sortOrder),
            StatusFilter: status,
            DefinitionIdFilter: definitionIdFilter,
            NameContains: nameFilter);
        var (total, pairs) = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _workflows.ListWithDisplayIdsPageAsync(uow, tenantId, pageQuery, innerCt),
            ct).ConfigureAwait(false);
        var items = pairs.Select(p => new ExecutionResponse
        {
            DisplayId = p.DisplayId ?? p.Workflow.ExecutionId.ToString(),
            ResourceId = p.Workflow.ExecutionId,
            GraphId = p.Workflow.DefinitionId.ToString("D"),
            Status = p.Workflow.Status,
            StartedAt = p.Workflow.StartedAt,
            UpdatedAt = p.Workflow.UpdatedAt,
            CancelRequested = p.Workflow.CancelRequested,
            RestartLost = p.Workflow.RestartLost
        }).ToList();

        return new PagedResult<ExecutionResponse>
        {
            Items = items,
            TotalCount = total,
            Offset = offset,
            Limit = limit,
            HasMore = offset + items.Count < total
        };
    }

    public async Task<ExecutionResponse> GetExecutionResponseAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var workflow = await _workflows.GetByIdAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                if (workflow is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, innerCt)
                    .ConfigureAwait(false) ?? workflow.ExecutionId.ToString("D");
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, workflow.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? workflow.DefinitionId.ToString("D");

                return new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = workflow.ExecutionId,
                    GraphId = graphId,
                    Status = workflow.Status,
                    StartedAt = workflow.StartedAt,
                    UpdatedAt = workflow.UpdatedAt,
                    CancelRequested = workflow.CancelRequested,
                    RestartLost = workflow.RestartLost
                };
            },
            ct).ConfigureAwait(false);
    }

    public async Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
        await EnsureWorkflowExistsAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);
        var row = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _workflows.GetSnapshotByExecutionIdAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        return row is null ? throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound) : row.GraphJson;
    }

    public Task EnsureWorkflowExistsAsync(string tenantId, Guid workflowId, CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var workflow = await _workflows.GetByIdAsync(uow, tenantId, workflowId, innerCt).ConfigureAwait(false);
                if (workflow is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
            },
            ct);

    public Task<string?> TryGetSnapshotGraphJsonByWorkflowIdAsync(Guid workflowId, CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var row = await _workflows.GetSnapshotByExecutionIdAsync(uow, workflowId, innerCt).ConfigureAwait(false);
                return row?.GraphJson;
            },
            ct);

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
            var existing = await _executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => _dedup.FindValidAsync(uow, key.DedupKey, dedupCheckTime, innerCt),
                ct).ConfigureAwait(false);
            if (existing is not null)
                return;
        }

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var workflow = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _workflows.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

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

        await _mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                await _workflows
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, projStatus, projCancel, projGraphJson, ctInner)
                    .ConfigureAwait(false);
                if (!skipCancelEventAppend)
                {
                    await _eventStore
                        .AppendAsync(uow, uuid.Value, EventStoreEventType.WorkflowCancelled, cancelPayload, ctInner)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _eventStore.TryAppendIfAbsentByClientEventAsync(
                        uow,
                        uuid.Value,
                        clientEventId,
                        EventStoreEventType.WorkflowCancelled,
                        cancelPayload,
                        ctInner).ConfigureAwait(false);
                }

                if (dedupKey is { } saveKey)
                {
                    var now = DateTime.UtcNow;
                    await _dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = StatusCodes.Status204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        ctInner).ConfigureAwait(false);
                }

                var nowUtc = DateTime.UtcNow;
                await _eventDeliveryDedup.TryUpdateStatusAsync(
                    uow,
                    tenantId,
                    uuid.Value,
                    clientEventId,
                    new EventDeliveryDedupStatusUpdate(
                        EventDeliveryDedupStatuses.Applied,
                        nowUtc,
                        AppliedAt: nowUtc,
                        ErrorCode: null),
                    ctInner).ConfigureAwait(false);
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
            var existing = await _executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => _dedup.FindValidAsync(uow, key.DedupKey, dedupCheckTime, innerCt),
                ct).ConfigureAwait(false);
            if (existing is not null)
                return;
        }

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var workflow = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _workflows.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

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

        await _mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                await _workflows
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, pubStatus, pubCancel, pubGraphJson, ctInner)
                    .ConfigureAwait(false);
                if (!skipEventAppend)
                {
                    await _eventStore
                        .AppendAsync(uow, uuid.Value, EventStoreEventType.EventPublished, publishedPayload, ctInner)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _eventStore.TryAppendIfAbsentByClientEventAsync(
                        uow,
                        uuid.Value,
                        clientEventId,
                        EventStoreEventType.EventPublished,
                        publishedPayload,
                        ctInner).ConfigureAwait(false);
                }

                if (dedupKey is { } saveKey)
                {
                    var now = DateTime.UtcNow;
                    await _dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = StatusCodes.Status204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        ctInner).ConfigureAwait(false);
                }

                var nowUtc = DateTime.UtcNow;
                await _eventDeliveryDedup.TryUpdateStatusAsync(
                    uow,
                    tenantId,
                    uuid.Value,
                    clientEventId,
                    new EventDeliveryDedupStatusUpdate(
                        EventDeliveryDedupStatuses.Applied,
                        nowUtc,
                        AppliedAt: nowUtc,
                        ErrorCode: null),
                    ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    public async Task<ExecutionViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await BuildWorkflowViewInternalAsync(tenantId, uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    public async Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct)
    {
        if (atSeq < 1)
            throw new ArgumentException("atSeq must be >= 1");

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var maxSeq = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _eventStore.GetMaxSeqAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
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

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var workflow = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _workflows.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (workflow is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false) ?? workflow.ExecutionId.ToString("D");
        var graphJson = await GetGraphJsonAsync(tenantId, idOrUuid, ct).ConfigureAwait(false);
        var patchNodes = ExecutionViewMapper.MapGraphPatchNodes(graphJson);

        var (items, hasMore) = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _eventStore.ListAfterSeqAsync(uow, uuid.Value, afterSeq, limit, innerCt),
            ct).ConfigureAwait(false);

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

    private async Task<ExecutionViewDto> BuildWorkflowViewInternalAsync(string tenantId, Guid uuid, string idOrUuidForDisplay, CancellationToken ct)
    {
        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var workflow = await _workflows.GetByIdAsync(uow, tenantId, uuid, innerCt).ConfigureAwait(false);
                if (workflow is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var snapshot = await _workflows.GetSnapshotByExecutionIdAsync(uow, uuid, innerCt).ConfigureAwait(false);
                if (snapshot is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuidForDisplay, innerCt)
                    .ConfigureAwait(false) ?? workflow.ExecutionId.ToString("D");
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, workflow.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? workflow.DefinitionId.ToString("D");
                return ExecutionViewMapper.BuildWorkflowView(workflow, snapshot.GraphJson, displayId, graphId);
            },
            ct).ConfigureAwait(false);
    }

    private static ExecutionResponse? DeserializeCachedExecutionResponse(CommandDedupRow existing)
    {
        if (string.IsNullOrEmpty(existing.ResponseBody))
            return null;
        return JsonDeserialize.TryDeserialize<ExecutionResponse>(existing.ResponseBody, CaseInsensitiveJsonSerializerOptions, out var deserialized)
            ? deserialized
            : null;
    }

    private async Task<ExecutionResponse?> TryGetIdempotentStartResponseAsync(
        string tenantId,
        CommandDedupKey? dedupKey,
        string requestHash,
        CancellationToken ct)
    {
        if (dedupKey is not { } key)
        {
            return null;
        }

        var dedupCheckTime = DateTime.UtcNow;
        var existing = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _dedup.FindValidAsync(uow, key.DedupKey, dedupCheckTime, innerCt),
            ct).ConfigureAwait(false);
        if (existing is not null)
        {
            var cached = DeserializeCachedExecutionResponse(existing);
            if (cached is not null)
            {
                return cached;
            }
        }

        var conflicting = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _dedup.FindValidConflictingRequestHashAsync(
                uow,
                tenantId,
                key.Endpoint,
                key.IdempotencyKey,
                requestHash,
                dedupCheckTime,
                innerCt),
            ct).ConfigureAwait(false);
        if (conflicting is not null)
        {
            throw new IdempotencyConflictException(
                "The same X-Idempotency-Key was used with a different request body.");
        }

        return null;
    }

    private Task<DefinitionVersionRow?> ResolveStartDefinitionVersionAsync(
        Guid tenantInternalId,
        Guid definitionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                if (request.DefinitionVersionId is { } versionId)
                {
                    var byId = await _definitions.GetVersionByIdAsync(uow, tenantInternalId, versionId, innerCt)
                        .ConfigureAwait(false);
                    return byId is not null && byId.DefinitionId == definitionId ? byId : null;
                }

                if (request.DefinitionVersion is { } versionNumber)
                {
                    return await _definitions
                        .GetVersionAsync(uow, tenantInternalId, definitionId, versionNumber, innerCt)
                        .ConfigureAwait(false);
                }

                var latest = await _definitions.GetLatestByIdAsync(uow, tenantInternalId, definitionId, innerCt)
                    .ConfigureAwait(false);
                return latest?.Version;
            },
            ct);

    private Task EnsureCanExecuteOnDefinitionAsync(
        Guid tenantInternalId,
        Guid definitionId,
        CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await _definitions
                    .ResolveProjectIdAsync(uow, tenantInternalId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await _projectAuth
                    .EnsureCanExecuteAsync(uow, tenantInternalId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);

    private CompiledWorkflowDefinition RestoreCompiledDefinitionFromVersion(DefinitionVersionRow versionRow)
    {
        try
        {
            return _compiler.RestoreFromStoredVersion(versionRow.SourceYaml, versionRow.CompiledJson);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Stored definition version is invalid.", ex);
        }
    }

    private static string ComputeStartRequestHash(StartExecutionRequest request)
    {
        var normalized = JsonSerializer.Serialize(
            new
            {
                definitionId = request.DefinitionId,
                definitionVersion = request.DefinitionVersion,
                definitionVersionId = request.DefinitionVersionId,
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
    private void EnsureEngineRuntimePresentForMutation(Guid workflowId, ExecutionRow workflow)
    {
        if (_engine.GetSnapshot(workflowId.ToString()) is not null)
            return;

        if (IsTerminalWorkflowProjectionStatus(workflow.Status))
        {
            throw new ArgumentException(
                "The workflow is already in a terminal state in the database projection, but there is no in-memory instance in this API process. Cancel or event delivery cannot be applied.");
        }

        throw new ArgumentException(
            "The workflow execution state is not loaded in this API process (for example after a restart). Commands cannot be applied while the in-memory runtime is missing.");
    }

    public async Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct)
    {
        var (status, cancelRequested, graphJson) = BuildProjectionFromEngineForQueue(workflowId);
        if (status is null || graphJson is null)
            return;

        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await _workflows
                    .UpdateExecutionAndSnapshotAsync(uow, workflowId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
            },
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

    private sealed class NoopWorkflowProjectionUpdateQueue : IExecutionProjectionUpdateQueue
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
    private void LogEventDeliveryDecision(EventDeliveryDecisionDetails details, Exception? exception = null)
    {
        switch (details.Decision)
        {
            case EventDeliveryLogDecisions.Failed:
            case EventDeliveryLogDecisions.BackoffBudgetExhausted:
                _logger.EventDeliveryDecisionError(exception!, details);
                break;
            case EventDeliveryLogDecisions.Retry:
            case EventDeliveryLogDecisions.AbortedTimeout:
                _logger.EventDeliveryDecisionWarning(exception!, details);
                break;
            default:
                _logger.EventDeliveryDecisionInformation(details);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Critical Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "イベント配送 dedup の再試行・ログ分岐を一箇所に集約している。")]
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
            ExecutionId = workflowId,
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
                await _executor.ExecuteReadCommittedAsync(
                    async (uow, innerCt) =>
                    {
                        await _eventDeliveryDedup.AddReceivedAsync(uow, row, innerCt).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(new EventDeliveryDecisionDetails
                {
                    TraceId = traceId,
                    TenantId = tenantId,
                    ExecutionId = workflowId,
                    ClientEventId = clientEventId,
                    Decision = EventDeliveryLogDecisions.Inserted,
                    Attempt = attempt,
                    ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                    ErrorCode = EventDeliveryLogErrorCodes.None,
                });
                return false;
            }
            catch (DbUpdateException dbUpdateException)
                when (EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdateException))
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(new EventDeliveryDecisionDetails
                {
                    TraceId = traceId,
                    TenantId = tenantId,
                    ExecutionId = workflowId,
                    ClientEventId = clientEventId,
                    Decision = EventDeliveryLogDecisions.DuplicateKey,
                    Attempt = attempt,
                    ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                    ErrorCode = EventDeliveryLogErrorCodes.UniqueViolation,
                });

                var existing = await _executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => _eventDeliveryDedup.FindAsync(uow, tenantId, workflowId, clientEventId, innerCt),
                    cancellationToken).ConfigureAwait(false);
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
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = workflowId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.AbortedTimeout,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
                    exception);
                throw;
            }
            catch (Exception exception) when (
                EventDeliveryRetryPolicy.IsTransientInfrastructureFailure(exception)
                && attempt < retryOptions.MaxAttempts)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = workflowId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.Retry,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
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
                            new EventDeliveryDecisionDetails
                            {
                                TraceId = traceId,
                                TenantId = tenantId,
                                ExecutionId = workflowId,
                                ClientEventId = clientEventId,
                                Decision = EventDeliveryLogDecisions.BackoffBudgetExhausted,
                                Attempt = attempt,
                                ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                                ErrorCode = EventDeliveryLogDecisions.BackoffBudgetExhausted,
                            },
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
            }
            catch (Exception exception)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = workflowId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.Failed,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
                    exception);
                throw;
            }
        }

        throw new InvalidOperationException("Event delivery insert retry loop ended unexpectedly.");
    }

}

