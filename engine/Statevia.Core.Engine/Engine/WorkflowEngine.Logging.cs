using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

public sealed partial class WorkflowEngine
{
    /// <summary>実行ログのメッセージ組み立てと <see cref="SafeLog"/> を一箇所に集約する。</summary>
    private sealed class WorkflowExecutionLogger
    {
        private readonly ILogger<WorkflowEngine> _logger;

        public WorkflowExecutionLogger(ILogger<WorkflowEngine> logger)
        {
            _logger = logger;
        }

        public void LogWorkflowStarted(string workflowId, string definitionName, string initialState) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "Workflow started WorkflowId={WorkflowId} DefinitionName={DefinitionName} InitialState={InitialState}",
                    workflowId,
                    definitionName,
                    initialState));

        public void LogWorkflowCancelRequested(string workflowId) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "Workflow cancel requested WorkflowId={WorkflowId}",
                    workflowId));

        public void LogWorkflowRunFailed(Exception exception, string workflowId, string definitionName) =>
            SafeLog(() =>
                _logger.LogError(
                    exception,
                    "Workflow run failed WorkflowId={WorkflowId} DefinitionName={DefinitionName}",
                    workflowId,
                    definitionName));

        public void LogStateScheduled(string workflowId, string stateName, string nodeId) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "State scheduled WorkflowId={WorkflowId} StateName={StateName} NodeId={NodeId}",
                    workflowId,
                    stateName,
                    nodeId));

        public void LogJoinStateScheduled(string workflowId, string joinStateName, string nodeId) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "State scheduled WorkflowId={WorkflowId} StateName={StateName} NodeId={NodeId} Kind=Join",
                    workflowId,
                    joinStateName,
                    nodeId));

        public void LogStateCompleted(string workflowId, string stateName, string nodeId, string fact, long elapsedMs) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "State completed WorkflowId={WorkflowId} StateName={StateName} NodeId={NodeId} Fact={Fact} ElapsedMs={ElapsedMs}",
                    workflowId,
                    stateName,
                    nodeId,
                    fact,
                    elapsedMs));

        public void LogJoinStateCompleted(string workflowId, string stateName, string nodeId, string fact) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "State completed WorkflowId={WorkflowId} StateName={StateName} NodeId={NodeId} Fact={Fact}",
                    workflowId,
                    stateName,
                    nodeId,
                    fact));

        public void LogStateExecuteFailed(Exception exception, string workflowId, string stateName, string nodeId, string errorType) =>
            SafeLog(() =>
                _logger.LogError(
                    exception,
                    "State execute failed WorkflowId={WorkflowId} StateName={StateName} NodeId={NodeId} ErrorType={ErrorType}",
                    workflowId,
                    stateName,
                    nodeId,
                    errorType));

        public void LogWorkflowCompleted(string workflowId, string definitionName) =>
            SafeLog(() =>
                _logger.LogInformation(
                    "Workflow completed WorkflowId={WorkflowId} DefinitionName={DefinitionName}",
                    workflowId,
                    definitionName));

        public void LogWorkflowTerminalFailure(string workflowId, string definitionName, string stateName, string fact) =>
            SafeLog(() =>
                _logger.LogError(
                    "Workflow terminal failure WorkflowId={WorkflowId} DefinitionName={DefinitionName} StateName={StateName} Fact={Fact}",
                    workflowId,
                    definitionName,
                    stateName,
                    fact));
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
