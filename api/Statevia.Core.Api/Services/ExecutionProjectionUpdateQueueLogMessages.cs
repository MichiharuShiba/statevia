using Microsoft.Extensions.Logging;

namespace Statevia.Core.Api.Services;

/// <summary>
/// <see cref="ExecutionProjectionUpdateQueueService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionProjectionUpdateQueueLogMessages
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Skip enqueue because workflow is dead-lettered ExecutionId={workflowId}")]
    public static partial void SkipEnqueueDeadLettered(this ILogger logger, Guid workflowId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Projection queue drain on shutdown timed out RemainingWorkflows={remainingWorkflows}")]
    public static partial void DrainShutdownTimeout(this ILogger logger, int remainingWorkflows);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Error,
        Message = "Projection queue moved workflow to dead-letter ExecutionId={workflowId} RetryCount={retryCount}")]
    public static partial void MovedToDeadLetter(this ILogger logger, Exception ex, Guid workflowId, int retryCount);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Projection queue processing failed. Retry scheduled ExecutionId={workflowId} Attempt={attempt}/{maxAttempts} DelayMs={delayMs}")]
    public static partial void ProcessingFailedRetryScheduled(
        this ILogger logger,
        Exception ex,
        Guid workflowId,
        int attempt,
        int maxAttempts,
        int delayMs);
}
