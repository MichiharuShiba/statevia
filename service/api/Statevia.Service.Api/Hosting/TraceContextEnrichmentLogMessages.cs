using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// <see cref="TraceContextEnrichmentMiddleware"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class TraceContextEnrichmentLogMessages
{
    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Information,
        Message = "HTTP trace enrich TraceId={traceId} ExecutionId={executionId} DefinitionId={definitionId} GraphDefinitionId={graphDefinitionId}")]
    public static partial void HttpTraceEnrich(
        this ILogger logger,
        string traceId,
        string? executionId,
        string? definitionId,
        string? graphDefinitionId);
}
