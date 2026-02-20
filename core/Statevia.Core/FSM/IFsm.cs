namespace Statevia.Core.FSM;

/// <summary>
/// 事実駆動型 FSM のインターフェース。
/// (状態名, 事実) から遷移結果を O(1) で評価します。
/// </summary>
public interface IFsm
{
    /// <summary>指定した状態で指定した事実が発生したときの遷移結果を評価します。</summary>
    TransitionResult Evaluate(string stateName, string fact);
}
