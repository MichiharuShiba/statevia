namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary>workflow builtin から子ワークフローを起動する。</summary>
internal interface IChildWorkflowRunner
{
    /// <summary>子ワークフローを起動する。</summary>
    /// <param name="request">起動リクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<ChildWorkflowResult> RunAsync(ChildWorkflowRequest request, CancellationToken ct);
}

/// <summary>子ワークフロー起動リクエスト。</summary>
/// <param name="DefinitionId">定義 ID（display または UUID）。</param>
/// <param name="Input">開始入力（任意）。</param>
/// <param name="ParentExecutionId">workflow Action を実行中の親実行 ID。</param>
internal sealed record ChildWorkflowRequest(
    string DefinitionId,
    object? Input,
    Guid ParentExecutionId);

/// <summary>子ワークフロー起動結果。</summary>
/// <param name="WorkflowId">実行 UUID 文字列。</param>
/// <param name="DisplayId">表示 ID。</param>
/// <param name="Status">実行状態。</param>
internal sealed record ChildWorkflowResult(
    string WorkflowId,
    string DisplayId,
    string Status);
