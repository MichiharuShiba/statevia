using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

internal sealed class ExecutionService : IExecutionService
{
    private readonly IExecutionEngine _engine;
    private readonly IDisplayIdService _displayIds;
    private readonly IDefinitionCompilerService _compiler;
    private readonly IIdGenerator _idGenerator;
    private readonly IExecutionRepository _executions;
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
    private readonly IForkChildExecutionCoordinator? _forkChildCoordinator;
    private readonly ExecutionQueryService _query;
    private readonly ExecutionIdempotencyService _idempotency;
    private readonly ExecutionProjectionOrchestrator _projection;
    private readonly ExecutionLifecycleCommandService _lifecycle;
    private readonly ExecutionWaitEventService _waitEvents;
    private readonly ExecutionCheckpointService _checkpoints;
    private readonly ExecutionOwnershipService _ownershipSessions;
    private readonly ExecutionRecoveryService _recovery;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "DI による明示的コンストラクタ注入。")]
    public ExecutionService(
        IExecutionEngine engine,
        IDisplayIdService displayIds,
        IDefinitionCompilerService compiler,
        IIdGenerator idGenerator,
        IExecutionRepository executions,
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
        IExecutionCheckpointStore checkpointStore,
        ExecutionQueryService query,
        ExecutionIdempotencyService idempotency,
        ExecutionProjectionOrchestrator projection,
        ExecutionLifecycleCommandService lifecycle,
        ExecutionWaitEventService waitEvents,
        ExecutionCheckpointService checkpoints,
        ExecutionOwnershipService ownershipSessions,
        ExecutionRecoveryService recovery,
        IExecutionWorkQueue? workQueue = null,
        ExecutionOwnershipTracker? ownershipTracker = null,
        IForkChildExecutionCoordinator? forkChildCoordinator = null)
    {
        _engine = engine;
        _displayIds = displayIds;
        _compiler = compiler;
        _idGenerator = idGenerator;
        _executions = executions;
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
        _query = query;
        _idempotency = idempotency;
        _projection = projection;
        _lifecycle = lifecycle;
        _waitEvents = waitEvents;
        _checkpoints = checkpoints;
        _ownershipSessions = ownershipSessions;
        _recovery = recovery;
        _forkChildCoordinator = forkChildCoordinator;
    }

    /// <inheritdoc />
    public Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _lifecycle.StartAsync(request, idempotencyKey, requestContext, ct);

    /// <inheritdoc />
    public Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        _lifecycle.ExecuteQueuedStartAsync(executionId, request, ct);

    public Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListPageQuery query,
        CancellationToken ct) =>
        _query.ListPagedAsync(query, ct);

    public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetExecutionResponseAsync(idOrUuid, ct);

    public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetGraphJsonAsync(idOrUuid, ct);

    public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
        _query.EnsureExecutionExistsAsync(executionId, ct);

    public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
        _query.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, ct);

    /// <inheritdoc />
    public Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _lifecycle.CancelAsync(idOrUuid, idempotencyKey, requestContext, ct);
        
    /// <inheritdoc />
    public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
        _checkpoints.PersistCheckpointKeepLoadedAsync(engineExecutionId, ct);

    /// <inheritdoc />
    public Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct) =>
        _ownershipSessions.BeginOwnedSessionAsync(executionId, workerId, leaseDuration, ct);

    /// <inheritdoc />
    public Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct) =>
        _ownershipSessions.RenewOwnedSessionLeaseAsync(executionId, leaseDuration, ct);

    /// <inheritdoc />
    public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) =>
        _ownershipSessions.EndOwnedSessionAsync(executionId, ct);

    /// <inheritdoc />
    public Task AbandonLocalOwnedSessionAsync(Guid executionId) =>
        _ownershipSessions.AbandonLocalOwnedSessionAsync(executionId);

    /// <inheritdoc />
    public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) =>
        _ownershipSessions.AwaitLocalExecutionLoadAsync(executionId, ct);

    /// <inheritdoc />
    public Task PublishEventAsync(
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _waitEvents.PublishEventAsync(idOrUuid, eventName, idempotencyKey, requestContext, ct);

    public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
        _query.GetExecutionViewAsync(idOrUuid, ct);

    public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
        _query.GetExecutionViewAtSeqAsync(idOrUuid, atSeq, ct);

    public Task<ExecutionEventsResponseDto> ListEventsAsync(
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct) =>
        _query.ListEventsAsync(idOrUuid, afterSeq, limit, ct);

    /// <inheritdoc />
    public Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _waitEvents.ResumeNodeAsync(
            idOrUuid,
            nodeId,
            resumeKey,
            idempotencyKey,
            requestContext,
            ct);

    /// <inheritdoc />
    public Task RecoverExecutionAsync(
        Guid executionId,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        _recovery.RecoverExecutionAsync(executionId, requestContext, ct);

    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
        _checkpoints.PersistCheckpointAndUnloadAsync(executionId, nodeId, ct);

    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct) =>
        _checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineExecutionId, nodeId, ct);

    public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
        _projection.UpdateProjectionFromEngineAsync(
            executionId,
            _checkpoints.ShouldDiscardRuntimeCheckpointAsync,
            _checkpoints.DiscardOrRefreshRuntimeCheckpointAsync,
            _lifecycle.UpsertRuntimeCheckpointAsync,
            ct);

}
