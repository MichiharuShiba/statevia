namespace Statevia.Core.Engine.Join;

/// <summary>
/// Fork/Join の Join 側を管理するトラッカー。
/// 状態の完了事実を記録し、Join ごとに構成された完了ポリシーを満たした Join 状態を検出します。
/// </summary>
public interface IJoinTracker
{
    /// <summary>状態の事実と出力を記録し、完了ポリシーを満たした Join 状態名を返します。満たしていなければ null。</summary>
    string? RecordFact(string stateName, string fact, object? output, string? nodeId = null);
    /// <summary>完了ポリシー判定に使われる依存状態のうち、Completed の出力を Join 実行入力として取得します。</summary>
    IReadOnlyDictionary<string, object?> GetJoinInputs(string joinStateName);
    /// <summary>Join 状態へ合流した依存ノードの実行ノード ID 一覧を取得します。</summary>
    IReadOnlyList<string> GetJoinSourceNodeIds(string joinStateName);
    /// <summary>Join 状態が完了ポリシーを満たして実行可能かを判定し、未実行なら開始済みとしてマークします。</summary>
    bool TryBeginJoinExecution(string joinStateName);
}
