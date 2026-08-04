using Microsoft.Extensions.Logging;

namespace Statevia.Runtime.Services;

internal static partial class DelayWaitSchedulerLogMessages
{
    [LoggerMessage(EventId = 3201, Level = LogLevel.Error, Message = "Delay wait scheduling iteration failed.")]
    public static partial void SchedulingIterationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Enqueued {Count} expired delay wait Resume work items.")]
    public static partial void ExpiredDelayWaitResumesEnqueued(this ILogger logger, int count);
}
