using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

public sealed partial class ExecutionEngine
{
    /// <summary>実行ログのメッセージ組み立てと <see cref="SafeLog"/> を一箇所に集約する。</summary>
    internal sealed class ExecutionEngineLogger
    {
        private readonly ILogger<ExecutionEngineLogger> _logger;

        public ExecutionEngineLogger(ILogger<ExecutionEngineLogger> logger)
        {
            _logger = logger;
        }

        /// <summary>StateContext 経由のログに Execution/State 文脈を付与するロガーを生成する。</summary>
        public StateContextLogger CreateStateContextLogger(string executionId, string stateName) =>
            new StateContextLogger(_logger, executionId, stateName);

        public void LogExecutionStarted(string executionId, string definitionName, string initialState) =>
            SafeLog(() =>
                ExecutionLog.ExecutionStarted(_logger, executionId, definitionName, initialState));

        public void LogExecutionCancelRequested(string executionId) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCancelRequested(_logger, executionId));

        public void LogExecutionRunFailed(Exception exception, string executionId, string definitionName) =>
            SafeLog(() =>
                ExecutionLog.ExecutionRunFailed(_logger, exception, executionId, definitionName));

        public void LogStateScheduled(string executionId, string stateName, string nodeId) =>
            SafeLog(() =>
                ExecutionLog.StateScheduled(_logger, executionId, stateName, nodeId));

        public void LogJoinStateScheduled(string executionId, string joinStateName, string nodeId) =>
            SafeLog(() =>
                ExecutionLog.JoinStateScheduled(_logger, executionId, joinStateName, nodeId));

        public void LogStateCompleted(string executionId, string stateName, string nodeId, string fact, long elapsedMs) =>
            SafeLog(() =>
                ExecutionLog.StateCompleted(_logger, executionId, stateName, nodeId, fact, elapsedMs));

        public void LogJoinStateCompleted(string executionId, string stateName, string nodeId, string fact) =>
            SafeLog(() =>
                ExecutionLog.JoinStateCompleted(_logger, executionId, stateName, nodeId, fact));

        public void LogStateExecuteFailed(Exception exception, string executionId, string stateName, string nodeId, string errorType) =>
            SafeLog(() =>
                ExecutionLog.StateExecuteFailed(_logger, exception, executionId, stateName, nodeId, errorType));

        public void LogExecutionCompleted(string executionId, string definitionName) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCompleted(_logger, executionId, definitionName));

        public void LogExecutionTerminalFailure(string executionId, string definitionName, string stateName, string fact) =>
            SafeLog(() =>
                ExecutionLog.ExecutionTerminalFailure(_logger, executionId, definitionName, stateName, fact));

        /// <summary>state input のフォールバック等、継続可能だが入力品質に注意が必要な状況（STV-405）。</summary>
        public void LogWarningInputEvaluation(string executionId, string stateName, string inputKey, string reason) =>
            SafeLog(() =>
                ExecutionLog.InputEvaluationWarning(_logger, executionId, stateName, inputKey, reason));

        /// <summary>条件遷移の path 解決警告（パス未解決・不正経路など）。</summary>
        public void LogWarningConditionPathResolution(string executionId, string stateName, string fact, string path, string reason) =>
            SafeLog(() =>
                ExecutionLog.ConditionPathResolutionWarning(_logger, executionId, stateName, fact, path, reason));

        /// <summary>FSM に次遷移がなく終端でもない停滞（STV-405）。</summary>
        public void LogWarningNoTransition(string executionId, string stateName, string fact) =>
            SafeLog(() =>
                ExecutionLog.NoTransition(_logger, executionId, stateName, fact));

        /// <summary>ノード完了通知ハンドラの実行失敗（エンジン進行は継続）。</summary>
        public void LogWarningNodeCompletedHandlerFailed(Exception exception, string executionId) =>
            SafeLog(() =>
                ExecutionLog.NodeCompletedHandlerFailed(_logger, exception, executionId));
    }

    /// <summary>
    /// 各ログ呼び出しに ExecutionId / StateName のスコープを付与する。
    /// </summary>
    internal sealed class StateContextLogger : ILogger
    {
        private readonly ILogger _logger;
        private readonly IReadOnlyDictionary<string, object?> _scope;

        public StateContextLogger(ILogger logger, string executionId, string stateName)
        {
            _logger = logger;
            _scope = new Dictionary<string, object?>
            {
                ["ExecutionId"] = executionId,
                ["StateName"] = stateName
            };
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state)!;

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            using var _ = _logger.BeginScope(_scope);
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    /// <summary>ログ プロバイダが例外を投げても実行遷移を壊さない（STV-404 Requirement 2b）。</summary>
#pragma warning disable CA1031 // あらゆるログ実装からの例外を握りつぶす仕様
    private static void SafeLog(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // intentional: observability must not fail the engine
        }
    }
#pragma warning restore CA1031
}
