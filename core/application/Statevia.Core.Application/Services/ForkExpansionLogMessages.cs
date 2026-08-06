using Microsoft.Extensions.Logging;

namespace Statevia.Core.Application.Services;

/// <summary>
/// Fork 物理子展開（Coordinator / HostHandler）用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ForkExpansionLogMessages
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Fork expansion attempt failed. Attempt={attempt} MaxAttempts={maxAttempts} ParentExecutionId={parentExecutionId} ForkNodeId={forkNodeId}")]
    public static partial void ForkExpansionAttemptFailed(
        this ILogger logger,
        Exception exception,
        int attempt,
        int maxAttempts,
        Guid parentExecutionId,
        string forkNodeId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Error,
        Message = "Fork expansion exhausted; failing parent and cancelling children. ParentExecutionId={parentExecutionId} ForkNodeId={forkNodeId}")]
    public static partial void ForkExpansionExhausted(
        this ILogger logger,
        Exception exception,
        Guid parentExecutionId,
        string forkNodeId);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Warning,
        Message = "Fork expansion skipped: invalid execution id. ExecutionId={executionId}")]
    public static partial void ForkExpansionSkippedInvalidExecutionId(this ILogger logger, string executionId);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Fork expansion skipped: parent execution not found. ParentExecutionId={parentExecutionId}")]
    public static partial void ForkExpansionSkippedParentNotFound(this ILogger logger, Guid parentExecutionId);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Warning,
        Message = "Fork expansion failed; parent marked Failed. ParentExecutionId={parentExecutionId} ForkNodeId={forkNodeId}")]
    public static partial void ForkExpansionFailedParentMarkedFailed(
        this ILogger logger,
        Guid parentExecutionId,
        string forkNodeId);
}
