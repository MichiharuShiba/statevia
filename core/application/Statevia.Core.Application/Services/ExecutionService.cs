namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="IExecutionService"/> の薄いファサード。ドメインサービスへ委譲する。
/// </summary>
/// <remarks>
/// <para>HTTP / Worker 契約の入口。ビジネス分岐は持たず、各ドメインサービスに委譲する。</para>
/// <para>Repository / Auth / Engine への直接依存は持たない（最終形）。</para>
/// </remarks>
/// <param name="queryService">Read-model 取得。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="lifecycle">Start / Cancel / QueuedStart。</param>
/// <param name="waitEvents">Publish / Resume。</param>
/// <param name="checkpoints">checkpoint Persist / Unload。</param>
/// <param name="ownershipSessions">Worker 所有セッション。</param>
/// <param name="recovery">Recover。</param>
internal sealed class ExecutionService(
    ExecutionQueryService queryService,
    ExecutionProjectionOrchestrator projection,
    ExecutionLifecycleCommandService lifecycle,
    ExecutionWaitEventService waitEvents,
    ExecutionCheckpointService checkpoints,
    ExecutionOwnershipService ownershipSessions,
    ExecutionRecoveryService recovery) : IExecutionService
{
    /// <inheritdoc />
    public Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        lifecycle.StartAsync(request, idempotencyKey, requestContext, ct);

    /// <inheritdoc />
    public Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        lifecycle.ExecuteQueuedStartAsync(executionId, request, ct);

    /// <inheritdoc />
    public Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListPageQuery query,
        CancellationToken ct) =>
        queryService.ListPagedAsync(query, ct);

    /// <inheritdoc />
    public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
        queryService.GetExecutionResponseAsync(idOrUuid, ct);

    /// <inheritdoc />
    public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) =>
        queryService.GetGraphJsonAsync(idOrUuid, ct);

    /// <inheritdoc />
    public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
        queryService.EnsureExecutionExistsAsync(executionId, ct);

    /// <inheritdoc />
    public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
        queryService.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, ct);

    /// <inheritdoc />
    public Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        lifecycle.CancelAsync(idOrUuid, idempotencyKey, requestContext, ct);

    /// <inheritdoc />
    public Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct) =>
        checkpoints.PersistCheckpointKeepLoadedAsync(engineExecutionId, ct);

    /// <inheritdoc />
    public Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct) =>
        ownershipSessions.BeginOwnedSessionAsync(executionId, workerId, leaseDuration, ct);

    /// <inheritdoc />
    public Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct) =>
        ownershipSessions.RenewOwnedSessionLeaseAsync(executionId, leaseDuration, ct);

    /// <inheritdoc />
    public Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct) =>
        ownershipSessions.EndOwnedSessionAsync(executionId, ct);

    /// <inheritdoc />
    public Task AbandonLocalOwnedSessionAsync(Guid executionId) =>
        ownershipSessions.AbandonLocalOwnedSessionAsync(executionId);

    /// <inheritdoc />
    public Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct) =>
        ownershipSessions.AwaitLocalExecutionLoadAsync(executionId, ct);

    /// <inheritdoc />
    public Task PublishEventAsync(
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        waitEvents.PublishEventAsync(idOrUuid, eventName, idempotencyKey, requestContext, ct);

    /// <inheritdoc />
    public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) =>
        queryService.GetExecutionViewAsync(idOrUuid, ct);

    /// <inheritdoc />
    public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
        queryService.GetExecutionViewAtSeqAsync(idOrUuid, atSeq, ct);

    /// <inheritdoc />
    public Task<ExecutionEventsResponseDto> ListEventsAsync(
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct) =>
        queryService.ListEventsAsync(idOrUuid, afterSeq, limit, ct);

    /// <inheritdoc />
    public Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct) =>
        waitEvents.ResumeNodeAsync(
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
        recovery.RecoverExecutionAsync(executionId, requestContext, ct);

    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct) =>
        checkpoints.PersistCheckpointAndUnloadAsync(executionId, nodeId, ct);

    /// <inheritdoc />
    public Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct) =>
        checkpoints.PersistCheckpointAndUnloadByEngineIdAsync(engineExecutionId, nodeId, ct);

    /// <inheritdoc />
    public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
        projection.UpdateProjectionFromEngineAsync(
            executionId,
            checkpoints.ShouldDiscardRuntimeCheckpointAsync,
            checkpoints.DiscardOrRefreshRuntimeCheckpointAsync,
            lifecycle.UpsertRuntimeCheckpointAsync,
            ct);
}
