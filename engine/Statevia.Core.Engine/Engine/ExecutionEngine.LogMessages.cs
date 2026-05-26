using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="ExecutionEngine"/> の実行ログ用 <see cref="LoggerMessage"/> 定義（CA1848）。
/// </summary>
public sealed partial class ExecutionEngine
{
    /// <summary>
    /// ソース生成による高パフォーマンス ログ（<see cref="ExecutionEngineLogger"/> が <see cref="SafeLog"/> 内から呼び出す）。
    /// <see cref="StateContextLogger"/> の <c>ILogger.Log</c> と名前が衝突しないよう <c>ExecutionLog</c> とする。
    /// </summary>
    private static partial class ExecutionLog
    {
        [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Execution started ExecutionId={ExecutionId} DefinitionName={DefinitionName} InitialState={InitialState}")]
        public static partial void ExecutionStarted(ILogger logger, string executionId, string definitionName, string initialState);

        [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Execution cancel requested ExecutionId={ExecutionId}")]
        public static partial void ExecutionCancelRequested(ILogger logger, string executionId);

        [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Execution run failed ExecutionId={ExecutionId} DefinitionName={DefinitionName}")]
        public static partial void ExecutionRunFailed(ILogger logger, Exception exception, string executionId, string definitionName);

        [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "State scheduled ExecutionId={ExecutionId} StateName={StateName} NodeId={NodeId}")]
        public static partial void StateScheduled(ILogger logger, string executionId, string stateName, string nodeId);

        [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "State scheduled ExecutionId={ExecutionId} StateName={StateName} NodeId={NodeId} Kind=Join")]
        public static partial void JoinStateScheduled(ILogger logger, string executionId, string stateName, string nodeId);

        [LoggerMessage(EventId = 5006, Level = LogLevel.Information, Message = "State completed ExecutionId={ExecutionId} StateName={StateName} NodeId={NodeId} Fact={Fact} ElapsedMs={ElapsedMs}")]
        public static partial void StateCompleted(ILogger logger, string executionId, string stateName, string nodeId, string fact, long elapsedMs);

        [LoggerMessage(EventId = 5007, Level = LogLevel.Information, Message = "State completed ExecutionId={ExecutionId} StateName={StateName} NodeId={NodeId} Fact={Fact}")]
        public static partial void JoinStateCompleted(ILogger logger, string executionId, string stateName, string nodeId, string fact);

        [LoggerMessage(EventId = 5008, Level = LogLevel.Error, Message = "State execute failed ExecutionId={ExecutionId} StateName={StateName} NodeId={NodeId} ErrorType={ErrorType}")]
        public static partial void StateExecuteFailed(ILogger logger, Exception exception, string executionId, string stateName, string nodeId, string errorType);

        [LoggerMessage(EventId = 5009, Level = LogLevel.Information, Message = "Execution completed ExecutionId={ExecutionId} DefinitionName={DefinitionName}")]
        public static partial void ExecutionCompleted(ILogger logger, string executionId, string definitionName);

        [LoggerMessage(EventId = 5010, Level = LogLevel.Error, Message = "Execution terminal failure ExecutionId={ExecutionId} DefinitionName={DefinitionName} StateName={StateName} Fact={Fact}")]
        public static partial void ExecutionTerminalFailure(ILogger logger, string executionId, string definitionName, string stateName, string fact);

        [LoggerMessage(EventId = 5011, Level = LogLevel.Warning, Message = "Input evaluation warning ExecutionId={ExecutionId} StateName={StateName} InputKey={InputKey} Reason={Reason}")]
        public static partial void InputEvaluationWarning(ILogger logger, string executionId, string stateName, string inputKey, string reason);

        [LoggerMessage(EventId = 5012, Level = LogLevel.Warning, Message = "Condition path resolution warning ExecutionId={ExecutionId} StateName={StateName} Fact={Fact} Path={Path} Reason={Reason}")]
        public static partial void ConditionPathResolutionWarning(ILogger logger, string executionId, string stateName, string fact, string path, string reason);

        [LoggerMessage(EventId = 5013, Level = LogLevel.Warning, Message = "No transition ExecutionId={ExecutionId} StateName={StateName} Fact={Fact}")]
        public static partial void NoTransition(ILogger logger, string executionId, string stateName, string fact);

        [LoggerMessage(EventId = 5014, Level = LogLevel.Warning, Message = "Node completion handler failed ExecutionId={ExecutionId}")]
        public static partial void NodeCompletedHandlerFailed(ILogger logger, Exception exception, string executionId);
    }
}
