using Statevia.Core.Engine.Abstractions;

namespace Statevia.Actions.Abstractions.Execution;

/// <summary>Action 実行ディスパッチャ（Catalog → Visibility → Policy → Backend）。</summary>
public interface IActionExecutor
{
    /// <summary>Action を実行する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="stateContext">Engine から渡される状態コンテキスト（InProcess で Backend へ伝播）。</param>
    /// <param name="runtimeInput">Engine が解決した状態入力（InProcess 用）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken);
}
