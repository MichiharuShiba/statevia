using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Configuration;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

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

    /// <summary>HTTP 201 Created。</summary>
    private const int HttpStatus201Created = 201;

    /// <summary>HTTP 204 No Content。</summary>
    private const int HttpStatus204NoContent = 204;

    private readonly IExecutionEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly ICommandDedupService _dedupService;
    private readonly IExecutionRepository _executions;
    private readonly IExecutionCursorRepository _executionCursors;
    private readonly IExecutionWaitRepository _executionWaits;
    private readonly IExecutionCheckpointStore _checkpointStore;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IExecutionMutationAuthorization _mutationAuth;
    private readonly IExecutionSecuritySnapshotFactory _snapshotFactory;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ICommandDedupRepository _dedup;
    private readonly IEventStoreRepository _eventStore;
    private readonly IEventDeliveryDedupRepository _eventDeliveryDedup;
    private readonly IDisplayIdWriteService _displayIdWrites;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IExecutionMutationPersistence _mutationPersistence;
    private readonly ILogger<ExecutionService> _logger;
    private readonly IOptions<EventDeliveryRetryOptions> _eventDeliveryRetryOptions;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IExecutionProjectionUpdateQueue _projectionUpdateQueue;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "DI による明示的コンストラクタ注入。")]
    public ExecutionService(
        IExecutionEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        ICommandDedupService dedupService,
        IExecutionRepository executions,
        IExecutionCursorRepository executionCursors,
        IExecutionWaitRepository executionWaits,
        IDefinitionRepository definitions,
        IProjectAuthorizationService projectAuth,
        IRuntimePermissionAuthorization runtimeAuth,
        IExecutionMutationAuthorization mutationAuth,
        IExecutionSecuritySnapshotFactory snapshotFactory,
        ITenantContextAccessor tenantContext,
        ICommandDedupRepository dedup,
        IEventStoreRepository eventStore,
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IDisplayIdWriteService displayIdWrites,
        ICoreTransactionExecutor executor,
        IExecutionMutationPersistence mutationPersistence,
        ILogger<ExecutionService> logger,
        IOptions<EventDeliveryRetryOptions> eventDeliveryRetryOptions,
        ICorrelationIdAccessor correlationIdAccessor,
        IExecutionProjectionUpdateQueue? projectionUpdateQueue = null,
        IExecutionCheckpointStore? checkpointStore = null)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _dedupService = dedupService;
        _executions = executions;
        _executionCursors = executionCursors;
        _executionWaits = executionWaits;
        _checkpointStore = checkpointStore ?? NoopExecutionCheckpointStore.Instance;
        _definitions = definitions;
        _projectAuth = projectAuth;
        _runtimeAuth = runtimeAuth;
        _mutationAuth = mutationAuth;
        _snapshotFactory = snapshotFactory;
        _tenantContext = tenantContext;
        _dedup = dedup;
        _eventStore = eventStore;
        _eventDeliveryDedup = eventDeliveryDedup;
        _displayIdWrites = displayIdWrites;
        _executor = executor;
        _mutationPersistence = mutationPersistence;
        _logger = logger;
        _eventDeliveryRetryOptions = eventDeliveryRetryOptions;
        _correlationIdAccessor = correlationIdAccessor;
        _projectionUpdateQueue = projectionUpdateQueue ?? NoopExecutionProjectionUpdateQueue.Instance;
    }

    public async Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantKey = _tenantContext.GetRequiredTenantKey();
        var requestHash = ComputeStartRequestHash(request);
        var dedupKey = _dedupService.Create(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path, requestHash);

        var cachedStart = await TryGetIdempotentStartResponseAsync(dedupKey, requestHash, ct).ConfigureAwait(false);
        if (cachedStart is not null)
        {
            return cachedStart;
        }

        var defUuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, request.DefinitionId!, ct).ConfigureAwait(false);
        if (defUuid is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var versionRow = await ResolveStartDefinitionVersionAsync(tenantId, defUuid.Value, request, ct).ConfigureAwait(false);
        if (versionRow is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        await EnsureCanExecuteOnDefinitionAsync(tenantId, defUuid.Value, ct).ConfigureAwait(false);

        var compiled = RestoreCompiledDefinitionFromVersion(versionRow);

        var executionId = _idGenerator.NewGuid();
        var engineId = executionId.ToString();
        _engine.Start(compiled, engineId, request.Input);

        var graphId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, defUuid.Value.ToString("D"), ct).ConfigureAwait(false)
            ?? defUuid.Value.ToString("D");
        var status = MapStatus(_engine.GetSnapshot(engineId));
        var graphJson = _engine.ExportExecutionGraph(engineId);

        return await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var createdAt = DateTime.UtcNow;
                var securitySnapshot = await _snapshotFactory
                    .CaptureForStartAsync(tenantId, defUuid.Value, createdAt, innerCt)
                    .ConfigureAwait(false);
                var securitySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(securitySnapshot);

                var startedPayload = JsonSerializer.Serialize(
                    new
                    {
                        definitionId = defUuid.Value.ToString(),
                        definitionVersionId = versionRow.DefinitionVersionId.ToString(),
                        definitionVersion = versionRow.Version,
                        tenantId,
                        startedByPrincipalId = securitySnapshot.StartedByPrincipalId,
                        permissionSetHash = securitySnapshot.PermissionSetHash,
                        securityModelVersion = securitySnapshot.SecurityModelVersion,
                        evaluationMode = securitySnapshot.EvaluationMode.ToString()
                    },
                    CamelCaseJsonSerializerOptions);

                var displayId = await _displayIdWrites
                    .AllocateAsync(uow, DisplayIdResourceTypes.Execution, executionId, innerCt)
                    .ConfigureAwait(false);

                var executionRow = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = tenantId,
                    DefinitionId = defUuid.Value,
                    DefinitionVersionId = versionRow.DefinitionVersionId,
                    Status = status,
                    StartedAt = createdAt,
                    UpdatedAt = createdAt,
                    CancelRequested = false,
                    RestartLost = false,
                    SecuritySnapshotJson = securitySnapshotJson
                };
                var snapshotRow = new ExecutionGraphSnapshotRow
                {
                    ExecutionId = executionId,
                    GraphJson = graphJson,
                    UpdatedAt = createdAt
                };

                var response = new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = executionId,
                    GraphId = graphId,
                    Status = status,
                    StartedAt = createdAt
                };

                await _executions.AddExecutionAndSnapshotAsync(uow, executionRow, snapshotRow, innerCt).ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        tenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);
                await _eventStore
                    .AppendAsync(uow, executionId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
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
                            StatusCode = HttpStatus201Created,
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
        ExecutionListPageQuery query,
        CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        ArgumentNullException.ThrowIfNull(query);

        var (total, pairs) = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.ListWithDisplayIdsPageAsync(uow, tenantId, query, innerCt),
            ct).ConfigureAwait(false);
        var items = pairs.Select(p => new ExecutionResponse
        {
            DisplayId = p.DisplayId ?? p.Execution.ExecutionId.ToString(),
            ResourceId = p.Execution.ExecutionId,
            GraphId = p.Execution.DefinitionId.ToString("D"),
            Status = p.Execution.Status,
            StartedAt = p.Execution.StartedAt,
            UpdatedAt = p.Execution.UpdatedAt,
            CancelRequested = p.Execution.CancelRequested,
            RestartLost = p.Execution.RestartLost
        }).ToList();

        return new PagedResult<ExecutionResponse>
        {
            Items = items,
            TotalCount = total,
            Offset = query.Page.Offset,
            Limit = query.Page.Limit,
            HasMore = query.Page.Offset + items.Count < total
        };
    }

    public async Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var execution = await _executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, innerCt)
                    .ConfigureAwait(false) ?? execution.ExecutionId.ToString("D");
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, execution.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? execution.DefinitionId.ToString("D");

                return new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = execution.ExecutionId,
                    GraphId = graphId,
                    Status = execution.Status,
                    StartedAt = execution.StartedAt,
                    UpdatedAt = execution.UpdatedAt,
                    CancelRequested = execution.CancelRequested,
                    RestartLost = execution.RestartLost
                };
            },
            ct).ConfigureAwait(false);
    }

    public async Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
        await EnsureExecutionExistsAsync(uuid.Value, ct).ConfigureAwait(false);
        var row = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetSnapshotByExecutionIdAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        return row is null ? throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound) : row.GraphJson;
    }

    public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var tenantId = _tenantContext.GetRequiredTenantId();
                var execution = await _executions.GetByIdAsync(uow, tenantId, executionId, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
            },
            ct);

    public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var row = await _executions.GetSnapshotByExecutionIdAsync(uow, executionId, innerCt).ConfigureAwait(false);
                return row?.GraphJson;
            },
            ct);

    public async Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var tenantKey = _tenantContext.GetRequiredTenantKey();
        var tenantId = _tenantContext.GetRequiredTenantId();
        var dedupKey = _dedupService.Create(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

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

        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        await EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

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
                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, projStatus, projCancel, projGraphJson, ctInner)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        uuid.Value,
                        tenantId,
                        projStatus,
                        projGraphJson,
                        nodeIdToClear: null,
                        ctInner)
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
                            StatusCode = HttpStatus204NoContent,
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
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var tenantKey = _tenantContext.GetRequiredTenantKey();
        var tenantId = _tenantContext.GetRequiredTenantId();
        var dedupKey = _dedupService.Create(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

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

        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        await EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        // PublishEvent 復帰時点ではグラフ上の Wait 完了が未反映になり得るため、発行前の nodeId で先行削除する。
        // グラフ解析失敗時は永続化済み wait 行からフォールバック解決する（Applied 時のみ）。
        var engineId = uuid.Value.ToString();
        var prePublishGraphJson = _engine.ExportExecutionGraph(engineId);

        // PublishEvent シム条件外（複数 Wait / 複数 events 等）は設計どおり 422。
        var publishApply = InvokeEngineWaitMutation(
            () => _engine.PublishEvent(engineId, eventName, clientEventId));
        var skipEventAppend = publishApply.IsAlreadyApplied;
        var nodeIdToClearFromGraph = publishApply.IsApplied
            ? TryResolveSoleActiveWaitNodeId(prePublishGraphJson)
            : null;

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
                var nodeIdToClear = nodeIdToClearFromGraph;
                if (publishApply.IsApplied && string.IsNullOrWhiteSpace(nodeIdToClear))
                {
                    var persistedWaits = await _executionWaits
                        .ListByExecutionIdAsync(uow, uuid.Value, ctInner)
                        .ConfigureAwait(false);
                    nodeIdToClear = TryResolveWaitNodeIdToClearFromPersisted(persistedWaits, eventName);
                }

                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, pubStatus, pubCancel, pubGraphJson, ctInner)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        uuid.Value,
                        tenantId,
                        pubStatus,
                        pubGraphJson,
                        nodeIdToClear,
                        ctInner)
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
                            StatusCode = HttpStatus204NoContent,
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

    public async Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    public async Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var maxSeq = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _eventStore.GetMaxSeqAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (maxSeq == 0)
            return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);

        if (atSeq > maxSeq)
            throw new NotFoundException("atSeq out of range");

        return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    public async Task<ExecutionEventsResponseDto> ListEventsAsync(
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct)
    {
        await EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var uuid = await _displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false) ?? execution.ExecutionId.ToString("D");
        var graphJson = await GetGraphJsonAsync(idOrUuid, ct).ConfigureAwait(false);
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

    public async Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resumeKey);

        var eventName = resumeKey.Trim();
        var tenantKey = _tenantContext.GetRequiredTenantKey();
        var tenantId = _tenantContext.GetRequiredTenantId();
        var dedupKey = _dedupService.Create(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

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

        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        await EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        // Resume 正本: nodeId + eventName。wait 行は nodeId で先行削除する。
        // 許可外イベント・非アクティブ Wait などは 422。
        var engineId = uuid.Value.ToString();
        InvokeEngineWaitMutation(() => _engine.ResumeWaitNode(engineId, nodeId, eventName));

        var (pubStatus, pubCancel, pubGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName, nodeId },
            CamelCaseJsonSerializerOptions);

        await _mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, pubStatus, pubCancel, pubGraphJson, ctInner)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        uuid.Value,
                        tenantId,
                        pubStatus,
                        pubGraphJson,
                        nodeIdToClear: nodeId,
                        ctInner)
                    .ConfigureAwait(false);
                await _eventStore
                    .AppendAsync(uow, uuid.Value, EventStoreEventType.EventPublished, publishedPayload, ctInner)
                    .ConfigureAwait(false);

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
                            StatusCode = HttpStatus204NoContent,
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

    private async Task<ExecutionViewDto> BuildExecutionViewInternalAsync(Guid uuid, string idOrUuidForDisplay, CancellationToken ct)
    {
        var tenantId = _tenantContext.GetRequiredTenantId();
        return await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var execution = await _executions.GetByIdAsync(uow, tenantId, uuid, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var snapshot = await _executions.GetSnapshotByExecutionIdAsync(uow, uuid, innerCt).ConfigureAwait(false);
                if (snapshot is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuidForDisplay, innerCt)
                    .ConfigureAwait(false) ?? execution.ExecutionId.ToString("D");
                var graphId = await _displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, execution.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? execution.DefinitionId.ToString("D");
                return ExecutionViewMapper.BuildExecutionView(execution, snapshot.GraphJson, displayId, graphId);
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
        CommandDedupKey? dedupKey,
        string requestHash,
        CancellationToken ct)
    {
        if (dedupKey is not { } key)
        {
            return null;
        }

        var tenantKey = _tenantContext.GetRequiredTenantKey();
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
                tenantKey,
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
        Guid tenantId,
        Guid definitionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => ResolveStartDefinitionVersionInUowAsync(uow, tenantId, definitionId, request, innerCt),
            ct);

    private async Task<DefinitionVersionRow?> ResolveStartDefinitionVersionInUowAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        StartExecutionRequest request,
        CancellationToken ct)
    {
        if (request.DefinitionVersionId is { } versionId)
            return await ResolveVersionByIdForAdmissionAsync(uow, tenantId, definitionId, versionId, ct)
                .ConfigureAwait(false);

        if (request.DefinitionVersion is { } versionNumber)
            return await ResolveVersionByNumberForAdmissionAsync(uow, tenantId, definitionId, versionNumber, ct)
                .ConfigureAwait(false);

        var latest = await _definitions
            .GetLatestForApiAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        return latest?.Version;
    }

    private async Task<DefinitionVersionRow?> ResolveVersionByIdForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        Guid versionId,
        CancellationToken ct)
    {
        var byId = await _definitions
            .GetVersionForExecutionByIdAsync(uow, tenantId, versionId, ct)
            .ConfigureAwait(false);
        if (byId is null || byId.DefinitionId != definitionId)
            return null;

        return await EnsureActiveParentForAdmissionAsync(uow, tenantId, definitionId, ct).ConfigureAwait(false)
            ? byId
            : null;
    }

    private async Task<DefinitionVersionRow?> ResolveVersionByNumberForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int versionNumber,
        CancellationToken ct)
    {
        var byNumber = await _definitions
            .GetVersionForExecutionAsync(uow, tenantId, definitionId, versionNumber, ct)
            .ConfigureAwait(false);
        if (byNumber is null)
            return null;

        return await EnsureActiveParentForAdmissionAsync(uow, tenantId, definitionId, ct).ConfigureAwait(false)
            ? byNumber
            : null;
    }

    private async Task<bool> EnsureActiveParentForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var active = await _definitions
            .GetLatestForApiAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        return active is not null;
    }

    private Task EnsureCanExecuteOnDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await _definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await _projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
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

    private static string MapStatus(ExecutionSnapshot? snapshot)
    {
        if (snapshot is null) return "Unknown";
        if (snapshot.IsCompleted) return "Completed";
        if (snapshot.IsCancelled) return "Cancelled";
        if (snapshot.IsFailed) return "Failed";
        return "Running";
    }

    /// <summary>
    /// 投影が終了状態かどうかを、<c>executions.status</c> の文字列値から判定する。
    /// </summary>
    private static bool IsTerminalExecutionProjectionStatus(string status) =>
        status is "Completed" or "Cancelled" or "Failed";

    private Task EnsureExecutionsReadAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsRead, ct);

    private Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        _runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    private Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return _mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }

    /// <summary>
    /// キャンセル／イベント発行の適用直前に、エンジンへ実行をロードする（未ロードなら checkpoint から hydrate）。
    /// </summary>
    private async Task EnsureEngineRuntimeLoadedForMutationAsync(
        Guid executionId,
        ExecutionRow execution,
        CancellationToken ct)
    {
        if (_engine.GetSnapshot(executionId.ToString()) is not null)
            return;

        if (IsTerminalExecutionProjectionStatus(execution.Status))
        {
            throw new ArgumentException(
                "The execution is already in a terminal state in the database projection, but there is no in-memory instance in this API process. Cancel or event delivery cannot be applied.");
        }

        var checkpointDocument = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _checkpointStore.GetByExecutionIdAsync(uow, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (checkpointDocument is null)
        {
            throw new ArgumentException(
                "The execution state is not loaded in this API process and no runtime checkpoint was found.");
        }

        var versionRow = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _definitions.GetVersionForExecutionByIdAsync(
                uow,
                execution.TenantId,
                execution.DefinitionVersionId,
                innerCt),
            ct).ConfigureAwait(false);
        if (versionRow is null)
        {
            throw new InvalidOperationException(
                $"Definition version '{execution.DefinitionVersionId}' was not found for execution '{executionId}'.");
        }

        var compiled = RestoreCompiledDefinitionFromVersion(versionRow);
        var checkpoint = JsonSerializer.Deserialize<ExecutionRuntimeCheckpoint>(
            checkpointDocument.CheckpointJson,
            CamelCaseJsonSerializerOptions)
            ?? throw new InvalidOperationException("Stored runtime checkpoint JSON is invalid.");

        _engine.ImportCheckpoint(compiled, checkpoint);
    }

    /// <summary>
    /// Wait 登録直後: チェックポイントを保存して Unload する。
    /// </summary>
    public async Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct)
    {
        var engineId = executionId.ToString("D");
        var checkpoint = _engine.ExportCheckpoint(engineId);
        if (checkpoint is null)
            return;

        var json = JsonSerializer.Serialize(checkpoint, CamelCaseJsonSerializerOptions);
        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await _checkpointStore.UpsertAsync(
                    uow,
                    new ExecutionCheckpointDocument
                    {
                        ExecutionId = executionId,
                        CheckpointJson = json,
                        SchemaVersion = checkpoint.SchemaVersion,
                        UpdatedAt = DateTime.UtcNow
                    },
                    innerCt).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        _engine.Unload(engineId);
        _logger.LogDebug(
            "Persisted runtime checkpoint and unloaded execution {ExecutionId} after Wait node {NodeId}.",
            executionId,
            nodeId);
    }

    public async Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct)
    {
        var (status, cancelRequested, graphJson) = BuildProjectionFromEngineForQueue(executionId);
        if (status is null || graphJson is null)
            return;

        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var execution = await _executions.GetByExecutionIdAsync(uow, executionId, innerCt).ConfigureAwait(false);
                if (execution is null)
                    return;

                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, executionId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        execution.TenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// executions / snapshot 更新と同一 tx 内で operational projection（cursor / durable wait）を同期する。
    /// </summary>
    private async Task SyncOperationalProjectionAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid tenantId,
        string status,
        string graphJson,
        string? nodeIdToClear,
        CancellationToken ct)
    {
        var snapshot = _engine.GetSnapshot(executionId.ToString());
        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            tenantId,
            status,
            snapshot,
            graphJson,
            nodeIdToClear);
        await ExecutionOperationalProjectionSync.SyncAsync(
            uow,
            _executionCursors,
            _executionWaits,
            request,
            ct).ConfigureAwait(false);
    }

    private (string Status, bool? CancelRequested, string GraphJson) BuildProjectionFromEngine(Guid executionId)
    {
        var engineId = executionId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot?.IsCancelled, graphJson);
    }

    private (string? status, bool? cancelRequested, string? graphJson) BuildProjectionFromEngineForQueue(Guid executionId)
    {
        var engineId = executionId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        if (snapshot is null)
        {
            _logger.SkipProjectionQueueUpdateDebug(executionId);
            return (null, null, null);
        }

        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot.IsCancelled, graphJson);
    }

    /// <summary>
    /// Engine Wait 操作の <see cref="InvalidOperationException"/> を API 422 に写像する。
    /// </summary>
    /// <remarks>
    /// PublishEvent シム条件外・Resume の許可外イベントなどは Engine が
    /// <see cref="InvalidOperationException"/> を投げる。クライアント向けには
    /// <see cref="ApiValidationException"/>（422）へ変換する。
    /// </remarks>
    private static T InvokeEngineWaitMutation<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiValidationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Engine Wait 操作の <see cref="InvalidOperationException"/> を API 422 に写像する。
    /// </summary>
    private static void InvokeEngineWaitMutation(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiValidationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// PublishEvent シム用。グラフ上の未完了 Wait がちょうど 1 件ならその nodeId を返す。
    /// </summary>
    /// <remarks>
    /// Engine の <c>PublishEvent</c> は単一アクティブ Wait のみ許可する。投影同期では
    /// 復帰直後にグラフ完了が遅延し得るため、発行前に取得した nodeId で wait 行を先行削除する。
    /// </remarks>
    /// <param name="graphJson"><see cref="IExecutionEngine.ExportExecutionGraph"/> の JSON。</param>
    /// <returns>唯一のアクティブ Wait の nodeId。0 件または 2 件以上なら null。</returns>
    private static string? TryResolveSoleActiveWaitNodeId(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(graphJson);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return FindSoleActiveWaitNodeId(nodes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>nodes 配列から唯一の未完了 Wait nodeId を返す（0 件または複数は null）。</summary>
    private static string? FindSoleActiveWaitNodeId(JsonElement nodes)
    {
        string? soleNodeId = null;
        foreach (var node in nodes.EnumerateArray())
        {
            if (!TryGetActiveWaitNodeId(node, out var nodeId))
                continue;

            if (soleNodeId is not null)
                return null;

            soleNodeId = nodeId;
        }

        return soleNodeId;
    }

    /// <summary>未完了 Wait ノードなら nodeId を取り出す。</summary>
    /// <remarks>
    /// 許可イベント未設定の Wait は durable wait 投影対象外のため除外する
    ///（<c>allowedEvents</c> または互換の <c>waitKey</c> が必要）。
    /// </remarks>
    private static bool TryGetActiveWaitNodeId(JsonElement node, out string? nodeId)
    {
        nodeId = null;
        if (!node.TryGetProperty("nodeType", out var nodeType)
            || !string.Equals(nodeType.GetString(), "Wait", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.TryGetProperty("completedAt", out var completedAt)
            && completedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return false;
        }

        if (!HasConfiguredWaitEvents(node))
            return false;

        if (!node.TryGetProperty("nodeId", out var nodeIdElement))
            return false;

        var value = nodeIdElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        nodeId = value;
        return true;
    }

    /// <summary>
    /// グラフ JSON の Wait ノードに許可イベント設定があるか
    ///（<c>allowedEvents</c> 優先、なければ非空の <c>waitKey</c>）。
    /// </summary>
    private static bool HasConfiguredWaitEvents(JsonElement node)
    {
        if (node.TryGetProperty("allowedEvents", out var allowedEvents)
            && allowedEvents.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in allowedEvents.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(entry.GetString()))
                {
                    return true;
                }
            }
        }

        return node.TryGetProperty("waitKey", out var waitKey)
            && waitKey.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(waitKey.GetString());
    }

    /// <summary>
    /// グラフから nodeId を解決できないときのフォールバック。永続化済み wait から削除対象を決める。
    /// </summary>
    /// <param name="waits">当該 execution の wait 行。</param>
    /// <param name="eventName">Publish したイベント名（前後空白は Trim して照合）。</param>
    /// <returns>
    /// wait が 1 件ならその nodeId。複数なら <paramref name="eventName"/> を許可する行がちょうど 1 件のときその nodeId。
    /// それ以外は null。
    /// </returns>
    internal static string? TryResolveWaitNodeIdToClearFromPersisted(
        IReadOnlyList<ExecutionWaitRow> waits,
        string eventName)
    {
        if (waits.Count == 0)
            return null;

        if (waits.Count == 1)
            return waits[0].NodeId;

        // AllowedEvents は投影時に Trim 済み。Publish 側の前後空白とも揃える。
        var normalizedEventName = eventName.Trim();
        string? soleMatchingNodeId = null;
        foreach (var wait in waits)
        {
            var allowsEvent = wait.AllowedEvents.Any(e =>
                string.Equals(e, normalizedEventName, StringComparison.OrdinalIgnoreCase));
            if (!allowsEvent)
                continue;

            if (soleMatchingNodeId is not null)
                return null;

            soleMatchingNodeId = wait.NodeId;
        }

        return soleMatchingNodeId;
    }

    private sealed class NoopExecutionProjectionUpdateQueue : IExecutionProjectionUpdateQueue
    {
        internal static readonly NoopExecutionProjectionUpdateQueue Instance = new();

        public Task EnqueueAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;

        public Task DrainAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoopExecutionCheckpointStore : IExecutionCheckpointStore
    {
        internal static readonly NoopExecutionCheckpointStore Instance = new();

        public Task UpsertAsync(
            ICoreUnitOfWork uow,
            ExecutionCheckpointDocument document,
            CancellationToken ct) => Task.CompletedTask;

        public Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) =>
            Task.FromResult<ExecutionCheckpointDocument?>(null);

        public Task DeleteAsync(
            ICoreUnitOfWork uow,
            Guid executionId,
            CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 現在リクエストの相関 ID（無ければ空文字）を返す。
    /// </summary>
    private string GetTraceIdOrEmpty() => _correlationIdAccessor.GetCorrelationId();

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
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        var retryOptions = _eventDeliveryRetryOptions.Value;
        var traceId = GetTraceIdOrEmpty();
        var tenantId = _tenantContext.GetRequiredTenantId();
        var acceptedAt = DateTime.UtcNow;
        var row = new EventDeliveryDedupRow
        {
            TenantId = tenantId,
            ExecutionId = executionId,
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
                    ExecutionId = executionId,
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
                    ExecutionId = executionId,
                    ClientEventId = clientEventId,
                    Decision = EventDeliveryLogDecisions.DuplicateKey,
                    Attempt = attempt,
                    ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                    ErrorCode = EventDeliveryLogErrorCodes.UniqueViolation,
                });

                var existing = await _executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => _eventDeliveryDedup.FindAsync(uow, tenantId, executionId, clientEventId, innerCt),
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
                        ExecutionId = executionId,
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
                        ExecutionId = executionId,
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
                                ExecutionId = executionId,
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
                        ExecutionId = executionId,
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
