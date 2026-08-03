using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="ExecutionOwnershipRecoveryHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ExecutionOwnershipRecoveryLogMessages
{
    [LoggerMessage(
        EventId = 3301,
        Level = LogLevel.Error,
        Message = "Expired ownership recovery iteration failed.")]
    public static partial void RecoveryIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3302,
        Level = LogLevel.Information,
        Message = "Enqueued {Count} recovery Resume work items for expired checkpoint ownership.")]
    public static partial void ExpiredOwnershipRecoveriesEnqueued(this ILogger logger, int count);
}
