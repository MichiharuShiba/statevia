using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary><see cref="DevelopmentNotificationSender"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
internal static partial class DevelopmentNotificationLogMessages
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "notification action skipped in Development (channel=email toLength={ToLength} subjectLength={SubjectLength})")]
    public static partial void NotificationSkippedInDevelopment(
        ILogger logger,
        int toLength,
        int subjectLength);
}
