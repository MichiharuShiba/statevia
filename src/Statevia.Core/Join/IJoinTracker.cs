namespace Statevia.Core.Join;

/// <summary>
/// Fork/Join の Join 側を管理するトラッカー。
/// 状態の完了事実を記録し、allOf が揃った Join 状態を検出します。
/// </summary>
public interface IJoinTracker
{
    /// <summary>状態の事実と出力を記録し、allOf が揃った Join 状態名を返します。揃っていなければ null。</summary>
    string? RecordFact(string stateName, string fact, object? output);
    /// <summary>Join 状態が実行する際の入力（依存状態の出力）を取得します。</summary>
    IReadOnlyDictionary<string, object?> GetJoinInputs(string joinStateName);
}
