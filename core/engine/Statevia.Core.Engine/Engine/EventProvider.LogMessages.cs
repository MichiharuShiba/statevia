using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

/// <summary><see cref="EventProvider"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
public sealed partial class EventProvider
{
    private static partial class EventProviderLog
    {
        [LoggerMessage(
            EventId = 5101,
            Level = LogLevel.Information,
            Message = "Domain event dispatched ExecutionId={ExecutionId} topic={Topic} payloadSummaryType={PayloadSummaryType}")]
        public static partial void DomainEventDispatched(
            ILogger logger,
            string executionId,
            string topic,
            string payloadSummaryType);
    }
}
