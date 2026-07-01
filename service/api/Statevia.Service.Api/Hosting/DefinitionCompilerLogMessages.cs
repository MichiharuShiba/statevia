using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Hosting;

/// <summary><see cref="DefinitionCompilerService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。</summary>
internal static partial class DefinitionCompilerLogMessages
{
    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "Action input schema is not registered for state {State} action {ActionId}. Compile continues in warning mode.")]
    public static partial void ActionInputSchemaMissing(
        ILogger logger,
        string state,
        string actionId);
}
