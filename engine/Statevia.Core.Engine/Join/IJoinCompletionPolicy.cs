namespace Statevia.Core.Engine.Join;

/// <summary>
/// Join 完了条件を判定する戦略インターフェース。
/// </summary>
public interface IJoinCompletionPolicy
{
    /// <summary>
    /// 観測結果 <paramref name="results"/> から、
    /// Join を進められるか判定する。
    /// </summary>
    bool IsSatisfied(IReadOnlyDictionary<string, JoinObservedState> results);
}
