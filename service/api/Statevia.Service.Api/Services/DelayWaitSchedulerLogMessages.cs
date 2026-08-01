using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="DelayWaitSchedulerHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class DelayWaitSchedulerLogMessages
{
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Error,
        Message = "Delay wait scheduling iteration failed.")]
    public static partial void SchedulingIterationFailed(this ILogger logger, Exception exception);
}
