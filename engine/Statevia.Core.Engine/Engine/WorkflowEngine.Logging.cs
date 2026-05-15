using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

public sealed partial class WorkflowEngine
{
    /// <summary>実行ログのメッセージ組み立てと <see cref="SafeLog"/> を一箇所に集約する。</summary>
    private sealed class WorkflowExecutionLogger
    {
        private readonly ILogger _logger;

        public WorkflowExecutionLogger(ILogger<WorkflowEngine> logger)
        {
            _logger = logger;
        }

        /// <summary>StateContext 経由のログに Workflow/State 文脈を付与するロガーを生成する。</summary>
        public StateContextLogger CreateStateContextLogger(string workflowId, string stateName) =>
            new StateContextLogger(_logger, workflowId, stateName);

        public void LogWorkflowStarted(string workflowId, string definitionName, string initialState) =>
            SafeLog(() =>
                WorkflowLog.WorkflowStarted(_logger, workflowId, definitionName, initialState));

        public void LogWorkflowCancelRequested(string workflowId) =>
            SafeLog(() =>
                WorkflowLog.WorkflowCancelRequested(_logger, workflowId));

        public void LogWorkflowRunFailed(Exception exception, string workflowId, string definitionName) =>
            SafeLog(() =>
                WorkflowLog.WorkflowRunFailed(_logger, exception, workflowId, definitionName));

        public void LogStateScheduled(string workflowId, string stateName, string nodeId) =>
            SafeLog(() =>
                WorkflowLog.StateScheduled(_logger, workflowId, stateName, nodeId));

        public void LogJoinStateScheduled(string workflowId, string joinStateName, string nodeId) =>
            SafeLog(() =>
                WorkflowLog.JoinStateScheduled(_logger, workflowId, joinStateName, nodeId));

        public void LogStateCompleted(string workflowId, string stateName, string nodeId, string fact, long elapsedMs) =>
            SafeLog(() =>
                WorkflowLog.StateCompleted(_logger, workflowId, stateName, nodeId, fact, elapsedMs));

        public void LogJoinStateCompleted(string workflowId, string stateName, string nodeId, string fact) =>
            SafeLog(() =>
                WorkflowLog.JoinStateCompleted(_logger, workflowId, stateName, nodeId, fact));

        public void LogStateExecuteFailed(Exception exception, string workflowId, string stateName, string nodeId, string errorType) =>
            SafeLog(() =>
                WorkflowLog.StateExecuteFailed(_logger, exception, workflowId, stateName, nodeId, errorType));

        public void LogWorkflowCompleted(string workflowId, string definitionName) =>
            SafeLog(() =>
                WorkflowLog.WorkflowCompleted(_logger, workflowId, definitionName));

        public void LogWorkflowTerminalFailure(string workflowId, string definitionName, string stateName, string fact) =>
            SafeLog(() =>
                WorkflowLog.WorkflowTerminalFailure(_logger, workflowId, definitionName, stateName, fact));

        /// <summary>state input のフォールバック等、継続可能だが入力品質に注意が必要な状況（STV-405）。</summary>
        public void LogWarningInputEvaluation(string workflowId, string stateName, string inputKey, string reason) =>
            SafeLog(() =>
                WorkflowLog.InputEvaluationWarning(_logger, workflowId, stateName, inputKey, reason));

        /// <summary>条件遷移の path 解決警告（パス未解決・不正経路など）。</summary>
        public void LogWarningConditionPathResolution(string workflowId, string stateName, string fact, string path, string reason) =>
            SafeLog(() =>
                WorkflowLog.ConditionPathResolutionWarning(_logger, workflowId, stateName, fact, path, reason));

        /// <summary>FSM に次遷移がなく終端でもない停滞（STV-405）。</summary>
        public void LogWarningNoTransition(string workflowId, string stateName, string fact) =>
            SafeLog(() =>
                WorkflowLog.NoTransition(_logger, workflowId, stateName, fact));

        /// <summary>ノード完了通知ハンドラの実行失敗（エンジン進行は継続）。</summary>
        public void LogWarningNodeCompletedHandlerFailed(Exception exception, string workflowId) =>
            SafeLog(() =>
                WorkflowLog.NodeCompletedHandlerFailed(_logger, exception, workflowId));
    }

    /// <summary>
    /// 各ログ呼び出しに WorkflowId / StateName のスコープを付与する。
    /// </summary>
    private sealed class StateContextLogger : ILogger
    {
        private readonly ILogger _logger;
        private readonly IReadOnlyDictionary<string, object?> _scope;

        public StateContextLogger(ILogger logger, string workflowId, string stateName)
        {
            _logger = logger;
            _scope = new Dictionary<string, object?>
            {
                ["WorkflowId"] = workflowId,
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

    /// <summary>ログ プロバイダが例外を投げてもワークフロー遷移を壊さない（STV-404 Requirement 2b）。</summary>
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
