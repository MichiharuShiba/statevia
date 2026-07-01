using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="ExecutionProjectionUpdateQueueService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionProjectionUpdateQueueLogMessages
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Skip enqueue because execution is dead-lettered ExecutionId={executionId}")]
    public static partial void SkipEnqueueDeadLettered(this ILogger logger, Guid executionId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Projection queue drain on shutdown timed out RemainingExecutions={remainingExecutions}")]
    public static partial void DrainShutdownTimeout(this ILogger logger, int remainingExecutions);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Error,
        Message = "Projection queue moved execution to dead-letter ExecutionId={executionId} RetryCount={retryCount}")]
    public static partial void MovedToDeadLetter(this ILogger logger, Exception ex, Guid executionId, int retryCount);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Projection queue processing failed. Retry scheduled ExecutionId={executionId} Attempt={attempt}/{maxAttempts} DelayMs={delayMs}")]
    public static partial void ProcessingFailedRetryScheduled(
        this ILogger logger,
        Exception ex,
        Guid executionId,
        int attempt,
        int maxAttempts,
        int delayMs);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Warning,
        Message = "Skip projection update because execution tenant was not found ExecutionId={executionId}")]
    public static partial void ExecutionTenantNotFound(this ILogger logger, Guid executionId);
}
