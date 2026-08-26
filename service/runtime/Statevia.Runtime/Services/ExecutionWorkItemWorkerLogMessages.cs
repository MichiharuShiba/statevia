using Microsoft.Extensions.Logging;

namespace Statevia.Runtime.Services;

/// <summary>
/// <see cref="ExecutionWorkItemWorkerHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionWorkItemWorkerLogMessages
{
    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Error,
        Message = "Execution work item worker iteration failed.")]
    public static partial void WorkerIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3302,
        Level = LogLevel.Error,
        Message = "Execution work item {WorkItemId} failed.")]
    public static partial void WorkItemFailed(this ILogger logger, Exception exception, Guid workItemId);

    [LoggerMessage(
        EventId = 3303,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} lost lease during processing; another worker may reclaim it.")]
    public static partial void WorkItemLeaseLost(this ILogger logger, Guid workItemId);

    [LoggerMessage(
        EventId = 3304,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} heartbeat failed.")]
    public static partial void WorkItemHeartbeatFailed(this ILogger logger, Exception exception, Guid workItemId);

    [LoggerMessage(
        EventId = 3305,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} could not acquire checkpoint ownership for execution {ExecutionId}.")]
    public static partial void WorkItemOwnershipAcquireFailed(
        this ILogger logger,
        Guid workItemId,
        Guid executionId);

    [LoggerMessage(
        EventId = 3306,
        Level = LogLevel.Warning,
        Message = "Execution work item {WorkItemId} failed to end owned session.")]
    public static partial void WorkItemSessionEndFailed(this ILogger logger, Exception exception, Guid workItemId);

    [LoggerMessage(
        EventId = 3308,
        Level = LogLevel.Information,
        Message = "Worker interrupted locally owned execution for Cancel. ExecutionId={executionId}")]
    public static partial void WorkerLocalCancelInterrupt(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3309,
        Level = LogLevel.Information,
        Message = "Worker lifecycle slots changed. ActiveLifecycleSlots={activeLifecycleSlots} MaxConcurrency={maxConcurrency}")]
    public static partial void WorkerLifecycleSlotsChanged(
        this ILogger logger,
        int activeLifecycleSlots,
        int maxConcurrency);
}
