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
}
