namespace Statevia.Core.Engine.Join;

/// <summary>
/// 既定の Join 完了条件戦略ファクトリー。
/// 現時点は allOf のみを返す。
/// </summary>
public sealed class JoinCompletionPolicyFactory : IJoinCompletionPolicyFactory
{
    /// <inheritdoc />
    public IJoinCompletionPolicy Create(string joinStateName, JoinConditionKind conditionKind, IReadOnlyList<string> dependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinStateName);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (dependencies.Count == 0)
        {
            throw new ArgumentException("Join dependencies must not be empty.", nameof(dependencies));
        }

        return conditionKind switch
        {
            JoinConditionKind.AllOf => new AllOfJoinCompletionPolicy(dependencies),
            _ => throw new NotSupportedException($"Unsupported join condition kind: {conditionKind}")
        };
    }
}
