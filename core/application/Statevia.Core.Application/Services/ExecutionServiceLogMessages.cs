using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="ExecutionService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionServiceLogMessages
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Skip projection queue update because runtime is missing for ExecutionId={executionId}")]
    public static partial void SkipProjectionQueueUpdateDebug(this ILogger logger, Guid executionId);

    /// <summary>
    /// Serializable 永続化リトライを記録する。
    /// </summary>
    public static void SerializablePersistRetry(this ILogger logger, SerializablePersistRetryDetails details) =>
        SerializablePersistRetry(
            logger,
            details.TraceId,
            details.ExecutionId,
            details.TenantId,
            details.ClientEventId,
            details.Attempt,
            details.MaxAttempts,
            details.DelayMs,
            details.FailureMessage);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "serializable_persist_retry TraceId={traceId} ExecutionId={executionId} TenantId={tenantId} ClientEventId={clientEventId} Attempt={attempt} MaxAttempts={maxAttempts} DelayMs={delayMs} FailureMessage={failureMessage}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    private static partial void SerializablePersistRetry(
        ILogger logger,
        string traceId,
        Guid executionId,
        Guid tenantId,
        Guid clientEventId,
        int attempt,
        int maxAttempts,
        int delayMs,
        string failureMessage);

    /// <summary>
    /// イベント配送 dedup の Information ログを記録する。
    /// </summary>
    public static void EventDeliveryDecisionInformation(
        this ILogger logger,
        EventDeliveryDecisionDetails details) =>
        EventDeliveryDecisionInformation(
            logger,
            details.TraceId,
            details.ExecutionId,
            details.TenantId,
            details.ClientEventId,
            details.Decision,
            details.Attempt,
            details.ElapsedMs,
            details.ErrorCode);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "event_delivery_decision TraceId={traceId} ExecutionId={executionId} TenantId={tenantId} ClientEventId={clientEventId} Decision={decision} Attempt={attempt} ElapsedMs={elapsedMs} ErrorCode={errorCode}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    private static partial void EventDeliveryDecisionInformation(
        ILogger logger,
        string traceId,
        Guid executionId,
        Guid tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);

    /// <summary>
    /// イベント配送 dedup の Warning ログを記録する。
    /// </summary>
    public static void EventDeliveryDecisionWarning(
        this ILogger logger,
        Exception ex,
        EventDeliveryDecisionDetails details) =>
        EventDeliveryDecisionWarning(
            logger,
            ex,
            details.TraceId,
            details.ExecutionId,
            details.TenantId,
            details.ClientEventId,
            details.Decision,
            details.Attempt,
            details.ElapsedMs,
            details.ErrorCode);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "event_delivery_decision TraceId={traceId} ExecutionId={executionId} TenantId={tenantId} ClientEventId={clientEventId} Decision={decision} Attempt={attempt} ElapsedMs={elapsedMs} ErrorCode={errorCode}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    private static partial void EventDeliveryDecisionWarning(
        ILogger logger,
        Exception ex,
        string traceId,
        Guid executionId,
        Guid tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);

    /// <summary>
    /// イベント配送 dedup の Error ログを記録する。
    /// </summary>
    public static void EventDeliveryDecisionError(
        this ILogger logger,
        Exception ex,
        EventDeliveryDecisionDetails details) =>
        EventDeliveryDecisionError(
            logger,
            ex,
            details.TraceId,
            details.ExecutionId,
            details.TenantId,
            details.ClientEventId,
            details.Decision,
            details.Attempt,
            details.ElapsedMs,
            details.ErrorCode);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Error,
        Message = "event_delivery_decision TraceId={traceId} ExecutionId={executionId} TenantId={tenantId} ClientEventId={clientEventId} Decision={decision} Attempt={attempt} ElapsedMs={elapsedMs} ErrorCode={errorCode}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    private static partial void EventDeliveryDecisionError(
        ILogger logger,
        Exception ex,
        string traceId,
        Guid executionId,
        Guid tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "Persisted runtime checkpoint and unloaded execution after start-sync. ExecutionId={executionId}")]
    public static partial void CheckpointUnloadedAfterStartSync(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Warning,
        Message = "ExportCheckpoint returned null while durable waits were present. ExecutionId={executionId}")]
    public static partial void ExportCheckpointNullWithDurableWaits(this ILogger logger, string executionId);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Warning,
        Message = "Skip keep-loaded checkpoint because engine execution id is not a Guid. EngineExecutionId={engineExecutionId}")]
    public static partial void SkipKeepLoadedCheckpointInvalidExecutionId(this ILogger logger, string engineExecutionId);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Warning,
        Message = "Keep-loaded checkpoint rejected by fencing. ExecutionId={executionId}")]
    public static partial void KeepLoadedCheckpointRejectedByFencing(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Debug,
        Message = "Unload during abandon. ExecutionId={executionId}")]
    public static partial void UnloadDuringAbandon(this ILogger logger, Exception exception, Guid executionId);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Warning,
        Message = "Timed out waiting for Wait node to complete. NodeId={nodeId} ExecutionId={executionId}")]
    public static partial void WaitNodeCompletionTimedOut(this ILogger logger, string nodeId, string executionId);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Warning,
        Message = "Timed out waiting for execution to quiesce after Wait resume. ExecutionId={executionId}")]
    public static partial void WaitResumeQuiesceTimedOut(this ILogger logger, string executionId);

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Warning,
        Message = "Skip checkpoint persist because engine execution id is not a Guid. EngineExecutionId={engineExecutionId}")]
    public static partial void SkipCheckpointPersistInvalidExecutionId(this ILogger logger, string engineExecutionId);

    [LoggerMessage(
        EventId = 3014,
        Level = LogLevel.Warning,
        Message = "ExportCheckpoint returned null; skip unload. ExecutionId={executionId} NodeId={nodeId}")]
    public static partial void ExportCheckpointNullSkipUnload(this ILogger logger, string executionId, string nodeId);

    [LoggerMessage(
        EventId = 3018,
        Level = LogLevel.Warning,
        Message = "Skip stale checkpoint persist that would overwrite newer Wait state. ExecutionId={executionId} NodeId={nodeId} StoredPendingWaits={storedPendingWaits} IncomingPendingWaits={incomingPendingWaits} StoredNodes={storedNodes} IncomingNodes={incomingNodes}")]
    public static partial void SkipStaleCheckpointPersist(
        this ILogger logger,
        Guid executionId,
        string nodeId,
        int storedPendingWaits,
        int incomingPendingWaits,
        int storedNodes,
        int incomingNodes);

    [LoggerMessage(
        EventId = 3019,
        Level = LogLevel.Information,
        Message = "Skip delayed Fork expansion unload; execution already progressed past Fork wait. ExecutionId={executionId} ForkNodeId={forkNodeId} ActiveStates={activeStates} PendingWaits={pendingWaits}")]
    public static partial void SkipForkExpansionUnloadAlreadyProgressed(
        this ILogger logger,
        Guid executionId,
        string forkNodeId,
        int activeStates,
        int pendingWaits);

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Information,
        Message = "Synced graph snapshot from runtime checkpoint after Join unload. ExecutionId={executionId} PendingWaits={pendingWaits} CheckpointNodes={checkpointNodes}")]
    public static partial void SyncedGraphSnapshotFromRuntimeCheckpoint(
        this ILogger logger,
        Guid executionId,
        int pendingWaits,
        int checkpointNodes);

    [LoggerMessage(
        EventId = 3015,
        Level = LogLevel.Warning,
        Message = "Unload after Wait because ownership fencing was lost. NodeId={nodeId} ExecutionId={executionId}")]
    public static partial void UnloadAfterWaitFencingLost(this ILogger logger, string nodeId, Guid executionId);

    [LoggerMessage(
        EventId = 3016,
        Level = LogLevel.Information,
        Message = "Persisted runtime checkpoint and unloaded execution after Wait. ExecutionId={executionId} NodeId={nodeId}")]
    public static partial void CheckpointUnloadedAfterWait(this ILogger logger, Guid executionId, string nodeId);

    [LoggerMessage(
        EventId = 3017,
        Level = LogLevel.Information,
        Message = "Persisted runtime checkpoint and unloaded execution after projection-sync. ExecutionId={executionId}")]
    public static partial void CheckpointUnloadedAfterProjectionSync(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3021,
        Level = LogLevel.Error,
        Message = "No-progress watchdog unloaded execution. ExecutionId={executionId} ElapsedMs={elapsedMs}")]
    public static partial void NoProgressWatchdogUnloaded(this ILogger logger, Guid executionId, long elapsedMs);

    [LoggerMessage(
        EventId = 3022,
        Level = LogLevel.Information,
        Message = "Terminated unstarted execution as Cancelled without hydrate. ExecutionId={executionId}")]
    public static partial void UnstartedCancelTerminated(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3023,
        Level = LogLevel.Information,
        Message = "Skipped queued Start because cancel was already accepted or execution is terminal. ExecutionId={executionId}")]
    public static partial void QueuedStartSkippedAfterCancel(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3025,
        Level = LogLevel.Information,
        Message = "Skipped queued Start because execution is already hydrated. ExecutionId={executionId}")]
    public static partial void QueuedStartSkippedAlreadyHydrated(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3024,
        Level = LogLevel.Error,
        Message = "Marked unstarted execution as Failed after a permanent work item failure. ExecutionId={executionId}")]
    public static partial void UnstartedPermanentFailureMarked(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3026,
        Level = LogLevel.Warning,
        Message = "Kept checkpoint after unclassified attempt limit on a hydrated execution. ExecutionId={executionId}")]
    public static partial void HydratedAttemptLimitKeptCheckpoint(this ILogger logger, Guid executionId);
}
