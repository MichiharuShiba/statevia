using Statevia.CoreEngine.Domain.Execution;

namespace Statevia.CoreEngine.Application.Guards;

/// <summary>Execution に対するガード。タスク 1.6 で拡張。reject 時は (code, message, details) を返す。IsCancelRequested は Domain.Extensions.ExecutionStateExtensions を使用。</summary>
public static class ExecutionGuards
{
    /// <summary>実行が終端（COMPLETED / FAILED / CANCELED）なら true。</summary>
    public static bool IsTerminal(this ExecutionState state) =>
        state.Status is ExecutionStatus.COMPLETED or ExecutionStatus.FAILED or ExecutionStatus.CANCELED;
}
