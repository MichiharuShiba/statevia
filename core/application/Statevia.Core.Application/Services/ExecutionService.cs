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
    /// <summary>Worker が 1 Load を待つときのポーリング間隔。</summary>
    private static readonly TimeSpan LocalExecutionLoadPollInterval = TimeSpan.FromMilliseconds(250);

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
    private readonly IExecutionWorkQueue? _workQueue;
    private readonly ExecutionOwnershipTracker _ownership;
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
    private readonly IForkChildExecutionCoordinator? _forkChildCoordinator;

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
        IExecutionCheckpointStore checkpointStore,
        IExecutionProjectionUpdateQueue? projectionUpdateQueue = null,
        IExecutionWorkQueue? workQueue = null,
        ExecutionOwnershipTracker? ownershipTracker = null,
        IForkChildExecutionCoordinator? forkChildCoordinator = null)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _dedupService = dedupService;
        _executions = executions;
        _executionCursors = executionCursors;
        _executionWaits = executionWaits;
        _checkpointStore = checkpointStore;
        _workQueue = workQueue;
        _ownership = ownershipTracker ?? new ExecutionOwnershipTracker();
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
        _forkChildCoordinator = forkChildCoordinator;
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

        // HTTP 受理経路: キューがある場合は Engine を回さず Start work item を投入する。
        // Worker 文脈（またはテストでキュー未注入）は従来どおり同一プロセスで実行する。
        if (_workQueue is not null
            && !string.Equals(requestContext.Method, "WORKER", StringComparison.Ordinal))
        {
            return await AcceptStartAndEnqueueAsync(
                    request,
                    requestHash,
                    dedupKey,
                    tenantId,
                    defUuid.Value,
                    versionRow,
                    ct)
                .ConfigureAwait(false);
        }

        return await ExecuteStartInProcessAsync(
                new ExecuteStartInProcessArgs(
                    request,
                    requestHash,
                    dedupKey,
                    tenantId,
                    defUuid.Value,
                    versionRow,
                    ExecutionId: null),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>実行行を受理し <see cref="ExecutionWorkItemKinds.Start"/> を enqueue する。</summary>
    private async Task<ExecutionResponse> AcceptStartAndEnqueueAsync(
        StartExecutionRequest request,
        string requestHash,
        CommandDedupKey? dedupKey,
        Guid tenantId,
        Guid definitionId,
        DefinitionVersionRow versionRow,
        CancellationToken ct)
    {
        var executionId = _idGenerator.NewSequentialGuid();
        var graphId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, definitionId.ToString("D"), ct).ConfigureAwait(false)
            ?? definitionId.ToString("D");
        const string emptyGraphJson = """{"nodes":[],"edges":[]}""";

        var response = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var createdAt = DateTime.UtcNow;
                var securitySnapshot = await _snapshotFactory
                    .CaptureForStartAsync(tenantId, definitionId, createdAt, innerCt)
                    .ConfigureAwait(false);
                var securitySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(securitySnapshot);
                var displayId = await _displayIdWrites
                    .AllocateAsync(uow, DisplayIdResourceTypes.Execution, executionId, innerCt)
                    .ConfigureAwait(false);

                var executionRow = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = tenantId,
                    DefinitionId = definitionId,
                    DefinitionVersionId = versionRow.DefinitionVersionId,
                    Status = ExecutionProjectionStatuses.Running,
                    StartedAt = createdAt,
                    UpdatedAt = createdAt,
                    CancelRequested = false,
                    RestartLost = false,
                    SecuritySnapshotJson = securitySnapshotJson
                };
                var snapshotRow = new ExecutionGraphSnapshotRow
                {
                    ExecutionId = executionId,
                    GraphJson = emptyGraphJson,
                    UpdatedAt = createdAt
                };
                var accepted = new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = executionId,
                    GraphId = graphId,
                    Status = ExecutionProjectionStatuses.Running,
                    StartedAt = createdAt
                };

                await _executions.AddExecutionAndSnapshotAsync(uow, executionRow, snapshotRow, innerCt).ConfigureAwait(false);

                var startedPayload = JsonSerializer.Serialize(
                    new
                    {
                        definitionId = definitionId.ToString(),
                        definitionVersionId = versionRow.DefinitionVersionId.ToString(),
                        definitionVersion = versionRow.Version,
                        tenantId,
                        startedByPrincipalId = securitySnapshot.StartedByPrincipalId,
                        permissionSetHash = securitySnapshot.PermissionSetHash,
                        securityModelVersion = securitySnapshot.SecurityModelVersion,
                        evaluationMode = securitySnapshot.EvaluationMode.ToString()
                    },
                    JsonSerializerProfiles.CamelCase);
                await _eventStore
                    .AppendAsync(uow, executionId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                    .ConfigureAwait(false);

                if (dedupKey is { } saveKey)
                {
                    var responseJson = JsonSerializer.Serialize(accepted, JsonSerializerProfiles.CamelCase);
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

                await _workQueue!.EnqueueAsync(
                    uow,
                    new ExecutionWorkItemRow
                    {
                        WorkItemId = _idGenerator.NewSequentialGuid(),
                        ExecutionId = executionId,
                        Kind = ExecutionWorkItemKinds.Start,
                        Payload = JsonSerializer.Serialize(
                            new ExecutionStartWorkItemPayload(executionId, request),
                            ExecutionWorkItemPayloadJson.Options),
                        AvailableAt = createdAt,
                        Attempts = 0,
                        CreatedAt = createdAt
                    },
                    innerCt).ConfigureAwait(false);

                return accepted;
            },
            ct).ConfigureAwait(false);

        return response;
    }

    /// <summary>同一プロセス内で Engine を起動し投影する（Worker / テスト互換）。</summary>
    private async Task<ExecutionResponse> ExecuteStartInProcessAsync(
        ExecuteStartInProcessArgs args,
        CancellationToken ct)
    {
        var compiled = RestoreCompiledDefinitionFromVersion(args.VersionRow);
        var resolvedExecutionId = args.ExecutionId ?? _idGenerator.NewSequentialGuid();
        var engineId = resolvedExecutionId.ToString();
        _engine.Start(compiled, engineId, args.Request.Input, args.Request.InitialState);

        var graphId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, args.DefinitionId.ToString("D"), ct).ConfigureAwait(false)
            ?? args.DefinitionId.ToString("D");

        try
        {
            var startResult = await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var createdAt = DateTime.UtcNow;
                    var startSnapshot = _engine.GetSnapshot(engineId)
                        ?? throw new InvalidOperationException(
                            $"Cannot project execution '{resolvedExecutionId}': engine snapshot is missing after Start.");
                    var status = MapStatus(startSnapshot);
                    var graphJson = _engine.ExportExecutionGraph(engineId);

                    ExecutionResponse response;
                    if (args.ExecutionId is null)
                    {
                        // 同一プロセス受理: セキュリティ snapshot と WorkflowStarted をここで確定する。
                        var securitySnapshot = await _snapshotFactory
                            .CaptureForStartAsync(args.TenantId, args.DefinitionId, createdAt, innerCt)
                            .ConfigureAwait(false);
                        var securitySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(securitySnapshot);

                        var startedPayload = JsonSerializer.Serialize(
                            new
                            {
                                definitionId = args.DefinitionId.ToString(),
                                definitionVersionId = args.VersionRow.DefinitionVersionId.ToString(),
                                definitionVersion = args.VersionRow.Version,
                                tenantId = args.TenantId,
                                startedByPrincipalId = securitySnapshot.StartedByPrincipalId,
                                permissionSetHash = securitySnapshot.PermissionSetHash,
                                securityModelVersion = securitySnapshot.SecurityModelVersion,
                                evaluationMode = securitySnapshot.EvaluationMode.ToString()
                            },
                            JsonSerializerProfiles.CamelCase);

                        var displayId = await _displayIdWrites
                            .AllocateAsync(uow, DisplayIdResourceTypes.Execution, resolvedExecutionId, innerCt)
                            .ConfigureAwait(false);

                        var executionRow = new ExecutionRow
                        {
                            ExecutionId = resolvedExecutionId,
                            TenantId = args.TenantId,
                            DefinitionId = args.DefinitionId,
                            DefinitionVersionId = args.VersionRow.DefinitionVersionId,
                            Status = status,
                            StartedAt = createdAt,
                            UpdatedAt = createdAt,
                            CancelRequested = false,
                            RestartLost = false,
                            SecuritySnapshotJson = securitySnapshotJson
                        };
                        var snapshotRow = new ExecutionGraphSnapshotRow
                        {
                            ExecutionId = resolvedExecutionId,
                            GraphJson = graphJson,
                            UpdatedAt = createdAt
                        };
                        response = new ExecutionResponse
                        {
                            DisplayId = displayId,
                            ResourceId = resolvedExecutionId,
                            GraphId = graphId,
                            Status = status,
                            StartedAt = createdAt
                        };

                        await _executions.AddExecutionAndSnapshotAsync(uow, executionRow, snapshotRow, innerCt).ConfigureAwait(false);
                        await _eventStore
                            .AppendAsync(uow, resolvedExecutionId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                            .ConfigureAwait(false);

                        if (args.DedupKey is { } saveKey)
                        {
                            var responseJson = JsonSerializer.Serialize(response, JsonSerializerProfiles.CamelCase);
                            await _dedup.SaveAsync(
                                uow,
                                new CommandDedupRow
                                {
                                    DedupKey = saveKey.DedupKey,
                                    Endpoint = saveKey.Endpoint,
                                    IdempotencyKey = saveKey.IdempotencyKey,
                                    RequestHash = args.RequestHash,
                                    StatusCode = HttpStatus201Created,
                                    ResponseBody = responseJson,
                                    CreatedAt = createdAt,
                                    ExpiresAt = createdAt.AddHours(24)
                                },
                                innerCt).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        // Worker: HTTP 受理時に snapshot / WorkflowStarted 済み。投影のみ更新する。
                        var existing = await _executions.GetByIdAsync(uow, args.TenantId, resolvedExecutionId, innerCt).ConfigureAwait(false)
                            ?? throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
                        var displayId = await _displayIds.GetDisplayIdAsync(
                                DisplayIdResourceTypes.Execution,
                                resolvedExecutionId.ToString("D"),
                                innerCt)
                            .ConfigureAwait(false)
                            ?? resolvedExecutionId.ToString("D");
                        response = new ExecutionResponse
                        {
                            DisplayId = displayId,
                            ResourceId = resolvedExecutionId,
                            GraphId = graphId,
                            Status = status,
                            StartedAt = existing.StartedAt
                        };
                        await _executions
                            .UpdateExecutionAndSnapshotAsync(
                                uow,
                                resolvedExecutionId,
                                status,
                                cancelRequested: existing.CancelRequested,
                                graphJson,
                                innerCt)
                            .ConfigureAwait(false);
                    }

                    await SyncOperationalProjectionAsync(
                            uow,
                            resolvedExecutionId,
                            args.TenantId,
                            status,
                            graphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    var checkpointSaved = false;
                    if (string.Equals(status, ExecutionProjectionStatuses.Running, StringComparison.Ordinal)
                        && ExecutionOperationalProjectionSync.HasDurableWaits(graphJson))
                    {
                        checkpointSaved = await UpsertRuntimeCheckpointAsync(uow, engineId, resolvedExecutionId, innerCt)
                            .ConfigureAwait(false);
                    }

                    return (Response: response, UnloadAfterCommit: checkpointSaved);
                },
                ct).ConfigureAwait(false);

            if (startResult.UnloadAfterCommit)
            {
                _engine.Unload(engineId);
                _logger.CheckpointUnloadedAfterStartSync(resolvedExecutionId);
            }

            return startResult.Response;
        }
        catch
        {
            // DB 反映前に失敗した場合、再試行で二重 Load しないようローカル Engine を捨てる。
            _engine.Unload(engineId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var versionRow = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _definitions.GetVersionForExecutionByIdAsync(
                uow,
                tenantId,
                execution.DefinitionVersionId,
                innerCt),
            ct).ConfigureAwait(false);
        if (versionRow is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        return await ExecuteStartInProcessAsync(
                new ExecuteStartInProcessArgs(
                    request,
                    RequestHash: string.Empty,
                    DedupKey: null,
                    tenantId,
                    execution.DefinitionId,
                    versionRow,
                    executionId),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Engine から checkpoint を export し、同一 UoW へ upsert する。
    /// </summary>
    /// <returns>export できて upsert したとき <see langword="true"/>。</returns>
    private async Task<bool> UpsertRuntimeCheckpointAsync(
        ICoreUnitOfWork uow,
        string engineExecutionId,
        Guid executionId,
        CancellationToken ct)
    {
        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
        {
            _logger.ExportCheckpointNullWithDurableWaits(engineExecutionId);
            return false;
        }

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;
        if (_ownership.TryGet(executionId, out _, out var generation))
        {
            return await _checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                    uow,
                    new ExecutionCheckpointRuntimeUpsert(
                        executionId,
                        generation,
                        json,
                        checkpoint.SchemaVersion,
                        updatedAt),
                    ct)
                .ConfigureAwait(false);
        }

        await _checkpointStore.UpsertAsync(
            uow,
            new ExecutionCheckpointDocument
            {
                ExecutionId = executionId,
                CheckpointJson = json,
                SchemaVersion = checkpoint.SchemaVersion,
                UpdatedAt = updatedAt
            },
            ct).ConfigureAwait(false);
        return true;
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
        if (row is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        if (_forkChildCoordinator is null)
            return row.GraphJson;

        return await _forkChildCoordinator
            .ComposeReadModelGraphJsonAsync(uuid.Value, row.GraphJson, ct)
            .ConfigureAwait(false);
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

        // HTTP 受理経路: キューがある場合は Engine を回さず Cancel work item を投入する。
        if (_workQueue is not null
            && !string.Equals(requestContext.Method, "WORKER", StringComparison.Ordinal))
        {
            await AcceptCancelAndEnqueueAsync(uuid.Value, dedupKey, ct).ConfigureAwait(false);
            return;
        }

        // WORKER / 同期 Cancel: 未終端子へ Cancel をカスケード（ネストは各層で再帰）。
        if (_forkChildCoordinator is not null)
        {
            await _forkChildCoordinator
                .CascadeCancelToRunningChildrenAsync(uuid.Value, ct)
                .ConfigureAwait(false);
        }

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
            JsonSerializerProfiles.CamelCase);

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

    /// <summary>Cancel work item を enqueue し、任意で dedup を保存する。</summary>
    private async Task AcceptCancelAndEnqueueAsync(
        Guid executionId,
        CommandDedupKey? dedupKey,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (dedupKey is { } saveKey)
                {
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
                        innerCt).ConfigureAwait(false);
                }

                await _workQueue!.EnqueueAsync(
                    uow,
                    new ExecutionWorkItemRow
                    {
                        WorkItemId = _idGenerator.NewSequentialGuid(),
                        ExecutionId = executionId,
                        Kind = ExecutionWorkItemKinds.Cancel,
                        Payload = "{}",
                        AvailableAt = now,
                        Attempts = 0,
                        CreatedAt = now
                    },
                    innerCt).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineExecutionId);
        if (!Guid.TryParse(engineExecutionId, out var executionId))
        {
            _logger.SkipKeepLoadedCheckpointInvalidExecutionId(engineExecutionId);
            return;
        }

        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
            return;

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;

        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (_ownership.TryGet(executionId, out _, out var generation))
                {
                    var ok = await _checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                            uow,
                            new ExecutionCheckpointRuntimeUpsert(
                                executionId,
                                generation,
                                json,
                                checkpoint.SchemaVersion,
                                updatedAt),
                            innerCt)
                        .ConfigureAwait(false);
                    if (!ok)
                    {
                        _logger.KeepLoadedCheckpointRejectedByFencing(executionId);
                    }

                    return;
                }

                await _checkpointStore.UpsertAsync(
                        uow,
                        new ExecutionCheckpointDocument
                        {
                            ExecutionId = executionId,
                            CheckpointJson = json,
                            SchemaVersion = checkpoint.SchemaVersion,
                            UpdatedAt = updatedAt
                        },
                        innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        var generation = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var seed = new ExecutionCheckpointDocument
                {
                    ExecutionId = executionId,
                    CheckpointJson = "{}",
                    SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                    UpdatedAt = DateTime.UtcNow
                };
                return await _checkpointStore.TryAcquireOwnershipAsync(
                        uow,
                        executionId,
                        workerId,
                        leaseUntil,
                        seed,
                        innerCt)
                    .ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        if (generation is long gen)
            _ownership.Set(executionId, workerId, gen);

        return generation;
    }

    /// <inheritdoc />
    public async Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        // Wait Unload 済みでトラッカーが空なら延長不要（失敗扱いにすると heartbeat が誤キャンセルする）。
        if (!_ownership.TryGet(executionId, out _, out var generation))
            return true;

        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        return await _executor.ExecuteReadCommittedAsync(
            (uow, innerCt) => _checkpointStore.TryRenewOwnershipLeaseAsync(
                uow,
                executionId,
                generation,
                leaseUntil,
                innerCt),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct)
    {
        if (!_ownership.TryGet(executionId, out _, out var generation))
            return;

        try
        {
            await _executor.ExecuteReadCommittedAsync(
                (uow, innerCt) => _checkpointStore.TryClearOwnershipAsync(
                    uow,
                    executionId,
                    generation,
                    innerCt),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _ownership.Clear(executionId);
        }
    }

    /// <inheritdoc />
    public Task AbandonLocalOwnedSessionAsync(Guid executionId)
    {
        _ownership.Clear(executionId);
        var engineId = executionId.ToString();
        try
        {
            _engine.Unload(engineId);
        }
#pragma warning disable CA1031 // ローカル放棄はベストエフォート。Unload 失敗で Worker を止めない。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.UnloadDuringAbandon(exception, executionId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct)
    {
        var engineId = executionId.ToString();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = _engine.GetSnapshot(engineId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
            {
                // 終端後に Unload すると投影キューが engine 不在でスキップし、
                // Start 直後の不完全 graph のまま Completed だけ残る。最終投影を先に確定する。
                await _projectionUpdateQueue.DrainAsync(executionId, ct).ConfigureAwait(false);
                await UpdateProjectionFromEngineAsync(executionId, ct).ConfigureAwait(false);
                _engine.Unload(engineId);
                return;
            }

            var checkpoint = _engine.ExportCheckpoint(engineId);
            // recovery hydrate 後など、PendingWaits があるのに Unload されていない場合がある。
            if (checkpoint is { PendingWaits.Count: > 0 }
                && !HasRunningNonWaitNodes(_engine.ExportExecutionGraph(engineId)))
            {
                var waitNodeId = checkpoint.PendingWaits[0].NodeId;
                await PersistCheckpointAndUnloadByEngineIdAsync(engineId, waitNodeId, ct)
                    .ConfigureAwait(false);
                return;
            }

            await Task.Delay(LocalExecutionLoadPollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Wait 以外で未完了の実行中ノードがあるか。</summary>
    private static bool HasRunningNonWaitNodes(string graphJson)
    {
        try
        {
            using var document = JsonDocument.Parse(graphJson);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return nodes.EnumerateArray().Any(node =>
            {
                var nodeType = node.TryGetProperty("nodeType", out var typeEl)
                    ? typeEl.GetString()
                    : null;
                if (string.Equals(nodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(nodeType, "EventWait", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (node.TryGetProperty("completedAt", out var completedAt)
                    && completedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    return false;
                }

                // startedAt があり未完了なら実行中とみなす。
                return node.TryGetProperty("startedAt", out var startedAt)
                    && startedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            });
        }
        catch (JsonException)
        {
            return false;
        }
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

        // 親 ID への PublishEvent も子 Wait へ振り替える（要件4）。
        if (await TryRedirectPublishEventToChildWaitAsync(
                uuid.Value,
                eventName,
                idempotencyKey,
                requestContext,
                ct).ConfigureAwait(false))
        {
            return;
        }

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
        await AwaitEngineReadyAfterWaitClearAsync(
                engineId,
                publishApply.IsApplied ? nodeIdToClearFromGraph : null,
                ct)
            .ConfigureAwait(false);

        var (pubStatus, pubCancel, pubGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName },
            JsonSerializerProfiles.CamelCase);

        await _mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                var nodeIdToClear = await ResolvePublishNodeIdToClearAsync(
                        uow,
                        uuid.Value,
                        publishApply.IsApplied,
                        nodeIdToClearFromGraph,
                        eventName,
                        ctInner)
                    .ConfigureAwait(false);

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
                await AppendEventPublishedAsync(
                        uow,
                        uuid.Value,
                        clientEventId,
                        publishedPayload,
                        skipEventAppend,
                        ctInner)
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

        // 永続化中に進んだ Engine 完了を取りこぼさない（中間 RUNNING 上書きの保険）。
        if (publishApply.IsApplied)
            await _projectionUpdateQueue.EnqueueAsync(uuid.Value, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Publish / Resume で Wait を消したあと、完了と後続 quiesce を待つ。
    /// </summary>
    private async Task AwaitEngineReadyAfterWaitClearAsync(
        string engineId,
        string? nodeIdToClear,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nodeIdToClear))
            return;

        await WaitForEngineWaitNodeCompletedAsync(engineId, nodeIdToClear, ct).ConfigureAwait(false);
        // Wait 完了直後の中間グラフ（次 Task RUNNING）を永続化すると完了投影を上書きし固着する。
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// グラフ解決できなかった場合に永続 wait 行から削除対象 nodeId を補完する。
    /// </summary>
    private async Task<string?> ResolvePublishNodeIdToClearAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        bool publishApplied,
        string? nodeIdFromGraph,
        string eventName,
        CancellationToken ct)
    {
        if (!publishApplied || !string.IsNullOrWhiteSpace(nodeIdFromGraph))
            return nodeIdFromGraph;

        var persistedWaits = await _executionWaits
            .ListByExecutionIdAsync(uow, executionId, ct)
            .ConfigureAwait(false);
        return TryResolveWaitNodeIdToClearFromPersisted(persistedWaits, eventName);
    }

    /// <summary>EventPublished を通常 append または clientEvent 冪等 append する。</summary>
    private async Task AppendEventPublishedAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        string publishedPayload,
        bool skipEventAppend,
        CancellationToken ct)
    {
        if (!skipEventAppend)
        {
            await _eventStore
                .AppendAsync(uow, executionId, EventStoreEventType.EventPublished, publishedPayload, ct)
                .ConfigureAwait(false);
            return;
        }

        await _eventStore.TryAppendIfAbsentByClientEventAsync(
            uow,
            executionId,
            clientEventId,
            EventStoreEventType.EventPublished,
            publishedPayload,
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
        // D6 / D6.1: GraphUpdated patch は合成グラフ由来。
        var graphJson = await GetGraphJsonAsync(idOrUuid, ct).ConfigureAwait(false);
        var patchNodes = ExecutionViewMapper.MapGraphPatchNodes(graphJson);

        var sourceRows = await LoadEventStoreRowsForTimelineAsync(uuid.Value, ct).ConfigureAwait(false);
        var (events, hasMore) = PhysicalForkEventTimelineComposer.ComposePage(
            sourceRows,
            displayId,
            patchNodes,
            afterSeq,
            limit);

        return new ExecutionEventsResponseDto { Events = events, HasMore = hasMore };
    }

    /// <summary>
    /// 親＋再帰子孫の event_store 行を読み取り合成用に集める（D6.1）。
    /// </summary>
    private async Task<IReadOnlyList<PhysicalForkEventTimelineComposer.SourceRow>> LoadEventStoreRowsForTimelineAsync(
        Guid rootExecutionId,
        CancellationToken ct)
    {
        var executionIds = new List<Guid> { rootExecutionId };
        if (_forkChildCoordinator is not null)
        {
            var descendants = await _forkChildCoordinator
                .ListDescendantExecutionIdsAsync(rootExecutionId, ct)
                .ConfigureAwait(false);
            executionIds.AddRange(descendants);
        }

        var rows = new List<PhysicalForkEventTimelineComposer.SourceRow>();
        foreach (var executionId in executionIds)
        {
            var isRoot = executionId == rootExecutionId;
            long afterSeq = 0;
            while (true)
            {
                var pageAfter = afterSeq;
                var (items, hasMore) = await _executor.ExecuteReadOnlyAsync(
                        (uow, innerCt) => _eventStore.ListAfterSeqAsync(
                            uow,
                            executionId,
                            pageAfter,
                            limit: 500,
                            innerCt),
                        ct)
                    .ConfigureAwait(false);

                foreach (var item in items)
                    rows.Add(new PhysicalForkEventTimelineComposer.SourceRow(item, isRoot));

                if (!hasMore || items.Count == 0)
                    break;

                afterSeq = items[^1].Seq;
                // 暴走防止（異常に長い event_store）。
                if (rows.Count >= 50_000)
                    break;
            }
        }

        return rows;
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

        // 親 ID へ届いた分岐 Wait は子 execution へ振り替える（要件4）。
        if (await TryRedirectResumeToChildWaitAsync(
                uuid.Value,
                nodeId,
                eventName,
                idempotencyKey,
                requestContext,
                ct).ConfigureAwait(false))
        {
            return;
        }

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        await _projectionUpdateQueue.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, _idGenerator);

        if (await TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        // 物理子終端の予約イベントは Wait Resume ではなく Join 再評価へ振る（D3）。
        if (string.Equals(eventName, ExecutionWaitEventNames.ChildCompleted, StringComparison.Ordinal))
        {
            await HandleChildCompletedResumeAsync(uuid.Value, execution, nodeId, ct).ConfigureAwait(false);
            return;
        }

        await EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        // Resume 正本: nodeId + eventName。wait 行は nodeId で先行削除する。
        // 許可外イベント・非アクティブ Wait などは 422。
        var engineId = uuid.Value.ToString();
        InvokeEngineWaitMutation(() => _engine.ResumeWaitNode(engineId, nodeId, eventName));
        // Resume のグラフ完了は非同期継続のため、投影取得前に Wait 完了を待つ。
        await WaitForEngineWaitNodeCompletedAsync(engineId, nodeId, ct).ConfigureAwait(false);
        // 続けて後続 Task が落ち着くまで待つ（Wait 完了直後の RUNNING 中間像の永続化を避ける）。
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        var (pubStatus, pubCancel, pubGraphJson) = BuildProjectionFromEngine(uuid.Value);
        var publishedPayload = JsonSerializer.Serialize(
            new { tenantId, name = eventName, nodeId },
            JsonSerializerProfiles.CamelCase);

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

                // Resume 後に durable Wait が残っていなければ checkpoint を破棄する。
                // 所有セッション中は lease 行を消さず runtime のみ更新する。
                if (ShouldDiscardRuntimeCheckpoint(pubStatus, pubGraphJson))
                {
                    await DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineId,
                            uuid.Value,
                            ctInner)
                        .ConfigureAwait(false);
                }

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

        // 永続化中に進んだ Engine 完了を取りこぼさない（中間 RUNNING 上書きの保険）。
        await _projectionUpdateQueue.EnqueueAsync(uuid.Value, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 親 ID の Resume を子 Wait へ振り替える。振り替えたとき true。
    /// </summary>
    private async Task<bool> TryRedirectResumeToChildWaitAsync(
        Guid requestedExecutionId,
        string nodeId,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        if (_forkChildCoordinator is null)
            return false;

        if (string.Equals(eventName, ExecutionWaitEventNames.ChildCompleted, StringComparison.Ordinal))
            return false;

        var delivery = await _forkChildCoordinator
            .TryResolveChildWaitDeliveryAsync(requestedExecutionId, nodeId, eventName, ct)
            .ConfigureAwait(false);
        if (delivery is null)
            return false;

        await ResumeNodeAsync(
                delivery.ExecutionId.ToString("D"),
                delivery.NodeId,
                delivery.EventName,
                idempotencyKey,
                requestContext,
                ct)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 親 ID の PublishEvent を子 Wait へ振り替える。振り替えたとき true。
    /// </summary>
    private async Task<bool> TryRedirectPublishEventToChildWaitAsync(
        Guid requestedExecutionId,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        if (_forkChildCoordinator is null)
            return false;

        var delivery = await _forkChildCoordinator
            .TryResolveChildWaitDeliveryAsync(requestedExecutionId, nodeId: null, eventName, ct)
            .ConfigureAwait(false);
        if (delivery is null)
            return false;

        await PublishEventAsync(
                delivery.ExecutionId.ToString("D"),
                delivery.EventName,
                idempotencyKey,
                requestContext,
                ct)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 物理子終端の予約 Resume を受け、親の Join を再評価する（D2-B / D3）。
    /// </summary>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="execution">親の投影行。</param>
    /// <param name="forkNodeId">Resume payload の nodeId（Fork 到達インスタンス）。</param>
    /// <param name="ct">キャンセル。</param>
    private async Task HandleChildCompletedResumeAsync(
        Guid parentExecutionId,
        ExecutionRow execution,
        string forkNodeId,
        CancellationToken ct)
    {
        if (_forkChildCoordinator is null)
            return;

        if (IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        var evaluation = await _forkChildCoordinator
            .EvaluateJoinAsync(parentExecutionId, forkNodeId, ct)
            .ConfigureAwait(false);

        switch (evaluation.Kind)
        {
            case ForkJoinEvaluationKind.Waiting:
                return;

            case ForkJoinEvaluationKind.Failed:
                await FailParentFromJoinAsync(
                        parentExecutionId,
                        forkNodeId,
                        evaluation.FailureStatus ?? ExecutionProjectionStatuses.Failed,
                        ct)
                    .ConfigureAwait(false);
                return;

            case ForkJoinEvaluationKind.Satisfied:
                await CompleteParentPhysicalJoinAsync(
                        parentExecutionId,
                        execution,
                        forkNodeId,
                        evaluation,
                        ct)
                    .ConfigureAwait(false);
                return;

            default:
                throw new InvalidOperationException($"Unknown fork join evaluation kind '{evaluation.Kind}'.");
        }
    }

    /// <summary>Join 失敗／キャンセルを親投影へ伝播する（残子 Cancel はタスク 6）。</summary>
    private async Task FailParentFromJoinAsync(
        Guid parentExecutionId,
        string forkNodeId,
        string failureStatus,
        CancellationToken ct)
    {
        _logger.ForkJoinFailedPropagated(parentExecutionId, forkNodeId, failureStatus);

        var snapshot = await _executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => _executions.GetSnapshotByExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);
        var graphJson = snapshot?.GraphJson ?? """{"nodes":[],"edges":[]}""";
        var cancelRequested = string.Equals(
            failureStatus,
            ExecutionProjectionStatuses.Cancelled,
            StringComparison.Ordinal);

        await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            failureStatus,
                            cancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);

                    await DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            parentExecutionId.ToString(),
                            parentExecutionId,
                            innerCt)
                        .ConfigureAwait(false);
                },
                ct)
            .ConfigureAwait(false);

        _engine.Unload(parentExecutionId.ToString());
    }

    /// <summary>Join 充足時に親 Engine で物理 Join を完了し投影を更新する。</summary>
    private async Task CompleteParentPhysicalJoinAsync(
        Guid parentExecutionId,
        ExecutionRow execution,
        string forkNodeId,
        ForkJoinEvaluation evaluation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evaluation.CandidateInputs);
        _logger.ForkJoinSatisfied(parentExecutionId, forkNodeId, evaluation.JoinState);

        await EnsureEngineRuntimeLoadedForMutationAsync(parentExecutionId, execution, ct).ConfigureAwait(false);

        var engineId = parentExecutionId.ToString();
        IReadOnlyList<PhysicalJoinContextFragment>? fragments = null;
        if (evaluation.ContextMerges is { Count: > 0 })
        {
            fragments = evaluation.ContextMerges
                .Select(m => new PhysicalJoinContextFragment(m.States, m.Vars))
                .ToList();
        }

        _engine.CompletePhysicalJoin(
            engineId,
            evaluation.JoinState,
            evaluation.CandidateInputs,
            fragments);
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        var (status, cancelRequested, graphJson) = BuildProjectionFromEngine(parentExecutionId);
        await _executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            parentExecutionId,
                            status,
                            cancelRequested,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);
                    await SyncOperationalProjectionAsync(
                            uow,
                            parentExecutionId,
                            execution.TenantId,
                            status,
                            graphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    if (ShouldDiscardRuntimeCheckpoint(status, graphJson))
                    {
                        await DiscardOrRefreshRuntimeCheckpointAsync(
                                uow,
                                engineId,
                                parentExecutionId,
                                innerCt)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await UpsertRuntimeCheckpointAsync(uow, engineId, parentExecutionId, innerCt)
                            .ConfigureAwait(false);
                    }
                },
                ct)
            .ConfigureAwait(false);

        await _projectionUpdateQueue.EnqueueAsync(parentExecutionId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecoverExecutionAsync(
        Guid executionId,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantId = _tenantContext.GetRequiredTenantId();
        var execution = await _executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => _executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        if (IsTerminalExecutionProjectionStatus(execution.Status))
            return;

        // Worker が BeginOwnedSession 済み前提。hydrate 後、完了フロンティア／PendingWaits から継続する。
        await EnsureEngineRuntimeLoadedForMutationAsync(executionId, execution, ct).ConfigureAwait(false);

        var engineId = executionId.ToString();
        // ImportCheckpoint が起動した継続（Wait 復元または完了フロンティア遷移）が落ち着くのを待つ。
        await WaitForEngineQuiescedAfterWaitAsync(engineId, ct).ConfigureAwait(false);

        var (status, cancelRequested, graphJson) = BuildProjectionFromEngine(executionId);
        await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                await _executions
                    .UpdateExecutionAndSnapshotAsync(uow, executionId, status, cancelRequested, graphJson, innerCt)
                    .ConfigureAwait(false);
                await SyncOperationalProjectionAsync(
                        uow,
                        executionId,
                        tenantId,
                        status,
                        graphJson,
                        nodeIdToClear: null,
                        innerCt)
                    .ConfigureAwait(false);

                if (ShouldDiscardRuntimeCheckpoint(status, graphJson))
                {
                    await DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineId,
                            executionId,
                            innerCt)
                        .ConfigureAwait(false);
                }
            },
            ct).ConfigureAwait(false);

        // Wait 復元のみで継続が無い場合、ここで Unload しないと Worker Await が解放されない。
        var restored = _engine.ExportCheckpoint(engineId);
        if (restored is { PendingWaits.Count: > 0 }
            && !HasRunningNonWaitNodes(_engine.ExportExecutionGraph(engineId)))
        {
            await PersistCheckpointAndUnloadByEngineIdAsync(engineId, restored.PendingWaits[0].NodeId, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resume 適用後、対象 Wait ノードのグラフ完了が反映されるまで短く待つ。
    /// </summary>
    /// <remarks>
    /// Engine 側の完了処理は <c>RunContinuationsAsynchronously</c> のため、
    /// <see cref="IExecutionEngine.ResumeWaitNode"/> 復帰直後の投影は古い Wait を含み得る。
    /// </remarks>
    private async Task WaitForEngineWaitNodeCompletedAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct)
    {
        const int maxAttempts = 200;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (_engine.GetSnapshot(engineExecutionId) is null)
                return;

            var graphJson = _engine.ExportExecutionGraph(engineExecutionId);
            if (!TryGetGraphNodeCompletion(graphJson, nodeId, out var completed))
            {
                // ノードが無い（テスト用 stub 等）場合は待たない。
                return;
            }

            if (completed)
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        _logger.WaitNodeCompletionTimedOut(nodeId, engineExecutionId);
    }

    /// <summary>
    /// Wait 再開後、後続状態の実行が落ち着くまで短く待つ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WaitForEngineWaitNodeCompletedAsync"/> は Wait の <c>completedAt</c> 時点で戻る。
    /// その直後は <c>ProcessFact</c> → 次 Task の <c>AddNode</c> が走り、グラフは
    /// 「Wait SUCCEEDED + 次 Task RUNNING」の中間像になり得る。
    /// </para>
    /// <para>
    /// Resume / Publish がこの中間像を serializable トランザクションで永続化すると、
    /// 後続の完了投影（noop 完了 → Completed）を上書きし、次ノードが RUNNING のまま固着する。
    /// </para>
    /// </remarks>
    private async Task WaitForEngineQuiescedAfterWaitAsync(
        string engineExecutionId,
        CancellationToken ct)
    {
        const int maxAttempts = 200;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = _engine.GetSnapshot(engineExecutionId);
            if (snapshot is null)
                return;

            if (snapshot.IsCompleted || snapshot.IsCancelled || snapshot.IsFailed)
                return;

            // ContinueRestoredWait は次 Task を ActiveStates に載せてから Wait を外すため、
            // 後続実行中は Count > 0。空なら後続未起動または完了済み。
            if (snapshot.ActiveStates.Count == 0)
                return;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        _logger.WaitResumeQuiesceTimedOut(engineExecutionId);
    }

    /// <summary>グラフ JSON 上の指定 nodeId の完了有無を取得する。</summary>
    /// <returns>ノードが存在するとき <see langword="true"/>。</returns>
    private static bool TryGetGraphNodeCompletion(string graphJson, string nodeId, out bool completed)
    {
        completed = false;
        if (string.IsNullOrWhiteSpace(graphJson) || string.IsNullOrWhiteSpace(nodeId))
            return false;

        try
        {
            using var document = JsonDocument.Parse(graphJson);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("nodeId", out var idProp)
                    || idProp.ValueKind != JsonValueKind.String
                    || !string.Equals(idProp.GetString(), nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                completed = node.TryGetProperty("completedAt", out var completedAt)
                    && completedAt.ValueKind is not JsonValueKind.Null
                    && completedAt.ValueKind is not JsonValueKind.Undefined;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
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

                var graphJson = snapshot.GraphJson;
                if (_forkChildCoordinator is not null)
                {
                    graphJson = await _forkChildCoordinator
                        .ComposeReadModelGraphJsonAsync(uuid, snapshot.GraphJson, innerCt)
                        .ConfigureAwait(false);
                }

                return ExecutionViewMapper.BuildExecutionView(execution, graphJson, displayId, graphId);
            },
            ct).ConfigureAwait(false);
    }

    private static ExecutionResponse? DeserializeCachedExecutionResponse(CommandDedupRow existing)
    {
        if (string.IsNullOrEmpty(existing.ResponseBody))
            return null;
        return JsonDeserialize.TryDeserialize<ExecutionResponse>(existing.ResponseBody, JsonSerializerProfiles.CaseInsensitive, out var deserialized)
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
            JsonSerializerProfiles.CamelCase);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string MapStatus(ExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.IsCompleted) return ExecutionProjectionStatuses.Completed;
        if (snapshot.IsCancelled) return ExecutionProjectionStatuses.Cancelled;
        if (snapshot.IsFailed) return ExecutionProjectionStatuses.Failed;
        return ExecutionProjectionStatuses.Running;
    }

    /// <summary>
    /// 投影が終了状態かどうかを、<c>executions.status</c> の文字列値から判定する。
    /// </summary>
    private static bool IsTerminalExecutionProjectionStatus(string status) =>
        ExecutionProjectionStatuses.IsTerminal(status);

    /// <summary>Running かつ durable Wait が無い／終端なら checkpoint を破棄する。</summary>
    /// <summary>
    /// durable Wait が不要（終端、または Running だが Wait 無し）のとき checkpoint 破棄候補。
    /// </summary>
    /// <remarks>
    /// 所有セッション中は <see cref="DiscardOrRefreshRuntimeCheckpointAsync"/> が削除を抑止する。
    /// BeginOwnedSession の seed 行は Wait 到達前から lease を保持するため。
    /// </remarks>
    private static bool ShouldDiscardRuntimeCheckpoint(string status, string graphJson) =>
        !string.Equals(status, ExecutionProjectionStatuses.Running, StringComparison.Ordinal)
        || !ExecutionOperationalProjectionSync.HasDurableWaits(graphJson);

    /// <summary>
    /// checkpoint を破棄するか、所有中なら runtime JSON のみ世代一致更新する。
    /// </summary>
    /// <returns>
    /// 投影同期後に Engine を Unload してよいとき <see langword="true"/>。
    /// 所有中の refresh や削除のみの場合は <see langword="false"/>。
    /// </returns>
    private async Task<bool> DiscardOrRefreshRuntimeCheckpointAsync(
        ICoreUnitOfWork uow,
        string engineExecutionId,
        Guid executionId,
        CancellationToken ct)
    {
        if (_ownership.TryGet(executionId, out _, out _))
        {
            // Worker が Load を保持中。lease 行は残し runtime だけ更新する（Unload しない）。
            _ = await UpsertRuntimeCheckpointAsync(uow, engineExecutionId, executionId, ct)
                .ConfigureAwait(false);
            return false;
        }

        await _checkpointStore.DeleteAsync(uow, executionId, ct).ConfigureAwait(false);
        return false;
    }

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
            JsonSerializerProfiles.CamelCase)
            ?? throw new InvalidOperationException("Stored runtime checkpoint JSON is invalid.");

        try
        {
            _engine.ImportCheckpoint(compiled, checkpoint);
        }
        catch (InvalidOperationException ex)
        {
            // 二重 hydrate 等はクライアント向け 422（ArgumentException）へ写像する。
            throw new ArgumentException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Wait 登録直後: チェックポイントを保存して Unload する。
    /// </summary>
    /// <remarks>
    /// Engine 辞書キーは <see cref="Guid.ToString()"/>（Start 時と同じ形式）を使う。
    /// Export できない場合は Warning を出し、Unload しない（インメモリ再開を維持する）。
    /// </remarks>
    public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
        PersistCheckpointAndUnloadCoreAsync(executionId.ToString(), executionId, nodeId, ct);

    /// <summary>
    /// Engine 実行 ID 文字列を正としてチェックポイントを保存し Unload する。
    /// </summary>
    /// <param name="engineExecutionId">Engine に渡した実行 ID（辞書キー）。</param>
    /// <param name="nodeId">Wait ノード ID（ログ用）。</param>
    /// <param name="ct">キャンセル。</param>
    public Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineExecutionId);
        if (!Guid.TryParse(engineExecutionId, out var executionId))
        {
            _logger.SkipCheckpointPersistInvalidExecutionId(engineExecutionId);
            return Task.CompletedTask;
        }

        return PersistCheckpointAndUnloadCoreAsync(engineExecutionId, executionId, nodeId, ct);
    }

    /// <summary>チェックポイント upsert ののち Engine から Unload する共通実装。</summary>
    private async Task PersistCheckpointAndUnloadCoreAsync(
        string engineExecutionId,
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
        {
            _logger.ExportCheckpointNullSkipUnload(engineExecutionId, nodeId);
            return;
        }

        // Unload 前に投影へ反映する（Unload 後は GetSnapshot が null になり投影キューが空振りする）。
        // snapshot 欠落時は Unknown/`{}` を書かず checkpoint のみ残す（マルチ Worker 競合で UI を壊さない）。
        var snapshot = _engine.GetSnapshot(engineExecutionId);
        string? status = null;
        bool? cancelRequested = null;
        string? graphJson = null;
        if (snapshot is not null)
        {
            status = MapStatus(snapshot);
            cancelRequested = snapshot.IsCancelled;
            graphJson = _engine.ExportExecutionGraph(engineExecutionId);
        }

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;
        var fenceLost = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var execution = await _executions.GetByExecutionIdAsync(uow, executionId, innerCt)
                    .ConfigureAwait(false);
                if (execution is not null && status is not null && graphJson is not null)
                {
                    await _executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            executionId,
                            status,
                            cancelRequested,
                            graphJson,
                            innerCt)
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
                }

                if (_ownership.TryGet(executionId, out _, out var generation))
                {
                    var upserted = await _checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                            uow,
                            new ExecutionCheckpointRuntimeUpsert(
                                executionId,
                                generation,
                                json,
                                checkpoint.SchemaVersion,
                                updatedAt),
                            innerCt)
                        .ConfigureAwait(false);
                    if (!upserted)
                        return true;

                    await _checkpointStore.TryClearOwnershipAsync(uow, executionId, generation, innerCt)
                        .ConfigureAwait(false);
                    return false;
                }

                await _checkpointStore.UpsertAsync(
                    uow,
                    new ExecutionCheckpointDocument
                    {
                        ExecutionId = executionId,
                        CheckpointJson = json,
                        SchemaVersion = checkpoint.SchemaVersion,
                        UpdatedAt = updatedAt
                    },
                    innerCt).ConfigureAwait(false);

                var existing = await _checkpointStore.GetByExecutionIdAsync(uow, executionId, innerCt)
                    .ConfigureAwait(false);
                if (existing?.OwnerWorkerId is not null)
                {
                    await _checkpointStore.TryClearOwnershipAsync(
                            uow,
                            executionId,
                            existing.OwnerGeneration,
                            innerCt)
                        .ConfigureAwait(false);
                }

                return false;
            },
            ct).ConfigureAwait(false);

        _ownership.Clear(executionId);
        _engine.Unload(engineExecutionId);

        if (fenceLost)
        {
            _logger.UnloadAfterWaitFencingLost(nodeId, executionId);
            return;
        }

        _logger.CheckpointUnloadedAfterWait(executionId, nodeId);
    }

    public async Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct)
    {
        var (status, cancelRequested, graphJson) = BuildProjectionFromEngineForQueue(executionId);
        if (status is null || graphJson is null)
            return;

        var engineExecutionId = executionId.ToString();
        var shouldUnloadAfterProjection = await _executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var execution = await _executions.GetByExecutionIdAsync(uow, executionId, innerCt).ConfigureAwait(false);
                if (execution is null)
                    return false;

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

                // durable Wait が無い／終端なら checkpoint を削除（Resume 後の残留や再 hydrate を防ぐ）。
                // ただし Worker 所有中は lease / fencing 行を消さない（Heartbeat が世代不一致で落ちる）。
                if (ShouldDiscardRuntimeCheckpoint(status, graphJson))
                {
                    return await DiscardOrRefreshRuntimeCheckpointAsync(
                            uow,
                            engineExecutionId,
                            executionId,
                            innerCt)
                        .ConfigureAwait(false);
                }

                var upserted = await UpsertRuntimeCheckpointAsync(uow, engineExecutionId, executionId, innerCt)
                    .ConfigureAwait(false);
                // 所有セッション中の Unload 境界は Worker（Await / EndOwnedSession）。投影からは落とさない。
                return upserted && !_ownership.TryGet(executionId, out _, out _);
            },
            ct).ConfigureAwait(false);

        string? childTerminalOutputJson = null;
        string? childTerminalStatesJson = null;
        string? childTerminalVarsJson = null;
        if (ExecutionProjectionStatuses.IsTerminal(status))
        {
            (childTerminalOutputJson, childTerminalStatesJson, childTerminalVarsJson) =
                TryGetChildTerminalContextJson(engineExecutionId);
        }

        if (shouldUnloadAfterProjection)
        {
            _engine.Unload(engineExecutionId);
            _logger.CheckpointUnloadedAfterProjectionSync(executionId);
        }

        if (ExecutionProjectionStatuses.IsTerminal(status) && _forkChildCoordinator is not null)
        {
            await _forkChildCoordinator
                .NotifyChildTerminalAsync(
                    executionId,
                    status,
                    childTerminalOutputJson,
                    childTerminalStatesJson,
                    childTerminalVarsJson,
                    ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>ロード中 Engine から終端 Context（output / states / vars）を JSON 文字列として取り出す。</summary>
    private (string? OutputJson, string? StatesJson, string? VarsJson) TryGetChildTerminalContextJson(
        string engineExecutionId)
    {
        var checkpoint = _engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
            return (null, null, null);

        var output = checkpoint.Context.WorkflowOutput;
        string? outputJson = null;
        if (output is not null
            && output.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            outputJson = output.Value.GetRawText();
        }

        string? statesJson = null;
        if (checkpoint.Context.States is { Count: > 0 })
        {
            statesJson = JsonSerializer.Serialize(
                checkpoint.Context.States.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value,
                    StringComparer.OrdinalIgnoreCase),
                JsonSerializerProfiles.CamelCase);
        }

        string? varsJson = null;
        var vars = checkpoint.Context.Vars;
        if (vars is not null
            && vars.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            varsJson = vars.Value.GetRawText();
        }

        return (outputJson, statesJson, varsJson);
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
            _idGenerator,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Engine 上の実行から投影用 status / graph を取得する。
    /// </summary>
    /// <remarks>
    /// snapshot 欠落時に <c>Unknown</c> と <c>{}</c> を永続すると一覧 UI が落ちるため、欠落は例外とする。
    /// 呼び出し側は事前に Load / Ensure 済みであること。キュー投影は
    /// <see cref="BuildProjectionFromEngineForQueue"/> を使い欠落をスキップする。
    /// </remarks>
    /// <exception cref="InvalidOperationException">Engine に snapshot が無いとき。</exception>
    private (string Status, bool? CancelRequested, string GraphJson) BuildProjectionFromEngine(Guid executionId)
    {
        var engineId = executionId.ToString();
        var snapshot = _engine.GetSnapshot(engineId);
        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Cannot project execution '{executionId}': engine snapshot is missing.");
        }

        var graphJson = _engine.ExportExecutionGraph(engineId);
        var status = MapStatus(snapshot);
        return (status, snapshot.IsCancelled, graphJson);
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

    /// <summary>同一プロセス Start の入力束。</summary>
    private sealed record ExecuteStartInProcessArgs(
        StartExecutionRequest Request,
        string RequestHash,
        CommandDedupKey? DedupKey,
        Guid TenantId,
        Guid DefinitionId,
        DefinitionVersionRow VersionRow,
        Guid? ExecutionId);
}
