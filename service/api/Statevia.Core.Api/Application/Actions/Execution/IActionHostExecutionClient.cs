using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>Action Host への OutOfProcess 実行クライアント。</summary>
internal interface IActionHostExecutionClient
{
    /// <summary>Action Host に実行を委譲する。</summary>
    /// <param name="request">Platform 実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}
