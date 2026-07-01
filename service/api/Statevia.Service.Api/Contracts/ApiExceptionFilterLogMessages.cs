using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// <see cref="ApiExceptionFilter"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class ApiExceptionFilterLogMessages
{
    /// <summary>契約外の未処理例外を Error で記録する。</summary>
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "Unhandled API exception")]
    public static partial void UnhandledApiException(this ILogger logger, Exception exception);
}
