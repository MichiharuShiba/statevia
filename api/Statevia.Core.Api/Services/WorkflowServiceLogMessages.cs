using Microsoft.Extensions.Logging;

namespace Statevia.Core.Api.Services;

/// <summary>
/// <see cref="WorkflowService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class WorkflowServiceLogMessages
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Skip projection queue update because runtime is missing for workflow {workflowId}")]
    public static partial void SkipProjectionQueueUpdateDebug(this ILogger logger, Guid workflowId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "serializable_persist_retry traceId={traceId} workflowId={workflowId} tenantId={tenantId} clientEventId={clientEventId} attempt={attempt} maxAttempts={maxAttempts} delayMs={delayMs} failureMessage={failureMessage}")]
    public static partial void SerializablePersistRetry(
        this ILogger logger,
        string traceId,
        Guid workflowId,
        string tenantId,
        Guid clientEventId,
        int attempt,
        int maxAttempts,
        int delayMs,
        string failureMessage);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "event_delivery_decision traceId={traceId} workflowId={workflowId} tenantId={tenantId} clientEventId={clientEventId} decision={decision} attempt={attempt} elapsedMs={elapsedMs} errorCode={errorCode}")]
    public static partial void EventDeliveryDecisionInformation(
        this ILogger logger,
        string traceId,
        Guid workflowId,
        string tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "event_delivery_decision traceId={traceId} workflowId={workflowId} tenantId={tenantId} clientEventId={clientEventId} decision={decision} attempt={attempt} elapsedMs={elapsedMs} errorCode={errorCode}")]
    public static partial void EventDeliveryDecisionWarning(
        this ILogger logger,
        Exception ex,
        string traceId,
        Guid workflowId,
        string tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Error,
        Message = "event_delivery_decision traceId={traceId} workflowId={workflowId} tenantId={tenantId} clientEventId={clientEventId} decision={decision} attempt={attempt} elapsedMs={elapsedMs} errorCode={errorCode}")]
    public static partial void EventDeliveryDecisionError(
        this ILogger logger,
        Exception ex,
        string traceId,
        Guid workflowId,
        string tenantId,
        Guid clientEventId,
        string decision,
        int attempt,
        long elapsedMs,
        string errorCode);
}
