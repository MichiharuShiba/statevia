namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// Join 供給経路の到達判定。呼び出し側は <c>error</c> / Failed 辺を後続に含めない。
/// </summary>
/// <remarks>
/// <para>Loader の枝→Join 照合と Engine の Fork 領域検証が同じ 1 経路判定を共有する。</para>
/// <para>供給辺は <c>next</c> / <c>wait.events</c> / <c>edges</c>。ネスト Fork は呼び出し側で内側 Join 出口へ正規化する。</para>
/// </remarks>
public static class JoinSupply
{
    /// <summary>供給後続だけを辿って <paramref name="from"/> から <paramref name="to"/> へ到達できるか。</summary>
    /// <param name="supplySuccessors">状態名 → 供給後続（Failed / error を含めてはならない）。</param>
    /// <param name="from">始点。</param>
    /// <param name="to">終点（通常は Join）。</param>
    /// <param name="doNotTraverse">通過してはならないノード（対象 <paramref name="to"/> 自身は除く。他 Join など）。</param>
    /// <returns>1 経路でも届けば <see langword="true"/>。</returns>
    /// <remarks>
    /// 他 Join を通過すると内側枝が外側 Join を誤供給するため、Loader は Join 名集合を渡す。
    /// </remarks>
    public static bool Reaches(
        IReadOnlyDictionary<string, IReadOnlyList<string>> supplySuccessors,
        string from,
        string to,
        IReadOnlySet<string>? doNotTraverse = null)
    {
        ArgumentNullException.ThrowIfNull(supplySuccessors);
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        var comparer = StringComparer.OrdinalIgnoreCase;
        if (comparer.Equals(from, to))
        {
            return true;
        }

        var visited = new HashSet<string>(comparer) { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            if (!supplySuccessors.TryGetValue(queue.Dequeue(), out var list))
            {
                continue;
            }

            if (list.Any(next => VisitSupplyNeighbor(next, to, comparer, visited, doNotTraverse, queue)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>供給後続を訪問し、終点なら true。障壁ならキューに載せない。</summary>
    private static bool VisitSupplyNeighbor(
        string next,
        string to,
        StringComparer comparer,
        HashSet<string> visited,
        IReadOnlySet<string>? doNotTraverse,
        Queue<string> queue)
    {
        if (string.IsNullOrWhiteSpace(next) || !visited.Add(next))
        {
            return false;
        }

        if (comparer.Equals(next, to))
        {
            return true;
        }

        if (doNotTraverse is null || !doNotTraverse.Contains(next))
        {
            queue.Enqueue(next);
        }

        return false;
    }
}
