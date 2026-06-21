using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary><see cref="ActionExecutionMode"/> の隔離度比較ヘルパー。</summary>
internal static class ActionExecutionModeStrictness
{
    /// <summary>隔離度の序数（大きいほど厳しい）。</summary>
    public static int Rank(ActionExecutionMode mode) =>
        mode switch
        {
            ActionExecutionMode.InProcess => 0,
            ActionExecutionMode.OutOfProcess => 1,
            ActionExecutionMode.Remote => 2,
            ActionExecutionMode.Container => 3,
            ActionExecutionMode.Wasm => 4,
            _ => 1,
        };

    /// <summary>より厳しい方の Mode を返す。</summary>
    public static ActionExecutionMode Max(ActionExecutionMode left, ActionExecutionMode right) =>
        Rank(left) >= Rank(right) ? left : right;
}
