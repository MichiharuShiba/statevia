namespace Statevia.Core.Engine.Join;

/// <summary>
/// Join 完了条件戦略を生成するファクトリー。
/// </summary>
public interface IJoinCompletionPolicyFactory
{
    /// <summary>
    /// Join 状態ごとの戦略を生成する。
    /// </summary>
    IJoinCompletionPolicy Create(string joinStateName, JoinConditionKind conditionKind, IReadOnlyList<string> dependencies);
}
