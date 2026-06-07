using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// <see cref="RequestLoggingMiddleware"/> 用の高頻度ログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class RequestLoggingLogMessages
{
    /// <summary>
    /// HTTP リクエスト開始ログを記録する。
    /// </summary>
    public static void HttpRequestStart(this ILogger logger, RequestLogStartDetails details) =>
        HttpRequestStart(
            logger,
            details.TraceId,
            details.Method,
            details.Path,
            details.QueryForLog,
            details.TenantId,
            details.UserAgent,
            details.RequestBody);

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Information,
        Message = "HTTP request start TraceId={traceId} Method={method} Path={path} Query={queryForLog} TenantId={tenantId} UserAgent={userAgent} RequestBody={requestBody}")]
    [SuppressMessage(
        "Major Code Smell",
        "S107:Methods should not have too many parameters",
        Justification = "LoggerMessage のテンプレートはプレースホルダごとに引数が必要。")]
    private static partial void HttpRequestStart(
        ILogger logger,
        string traceId,
        string method,
        string path,
        string queryForLog,
        Guid? tenantId,
        string? userAgent,
        string? requestBody);

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Error,
        Message = "HTTP request unhandled exception TraceId={traceId} ExceptionType={exceptionType} Message={message}")]
    public static partial void HttpRequestUnhandledException(
        this ILogger logger,
        Exception ex,
        string traceId,
        string? exceptionType,
        string message);

    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Information,
        Message = "HTTP request complete TraceId={traceId} StatusCode={statusCode} ElapsedMs={elapsedMs} ResponseSize={responseSize} ResponseBody={responseBody}")]
    public static partial void HttpRequestComplete(
        this ILogger logger,
        string traceId,
        int statusCode,
        long elapsedMs,
        long? responseSize,
        string? responseBody);
}
