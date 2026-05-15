using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Join;

/// <summary>
/// 全依存状態が Completed のときのみ Join を成立させる戦略（現行互換）。
/// </summary>
public sealed class AllOfJoinCompletionPolicy : IJoinCompletionPolicy
{
    private readonly IReadOnlyList<string> _dependencies;

    /// <summary>
    /// 依存状態名の一覧を指定して All-Of 完了ポリシーを構築する。
    /// </summary>
    /// <param name="dependencies">Join が待つ依存状態名（大文字小文字は定義に従う）。</param>
    public AllOfJoinCompletionPolicy(IReadOnlyList<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;
    }

    /// <inheritdoc />
    public bool IsSatisfied(IReadOnlyDictionary<string, JoinObservedState> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        foreach (var dependency in _dependencies)
        {
            if (!results.TryGetValue(dependency, out var observed) || observed.Fact != Fact.Completed)
            {
                return false;
            }
        }

        return true;
    }
}
