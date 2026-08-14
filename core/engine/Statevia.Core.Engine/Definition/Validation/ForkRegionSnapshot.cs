namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// Fork 領域検証用の共有スナップショット。1 回の Validate で一度だけ構築する。
/// </summary>
internal sealed class ForkRegionSnapshot
{
    /// <summary>制御辺（Failed 含む）。</summary>
    public required IReadOnlyList<ControlEdge> Edges { get; init; }

    /// <summary>始点 → 出辺。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> Outgoing { get; init; }

    /// <summary>終点 → 入辺。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<ControlEdge>> Incoming { get; init; }

    /// <summary>Fork 状態 → 枝先頭。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Forks { get; init; }

    /// <summary>Join 状態 → all。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Joins { get; init; }

    /// <summary>対応が取れた Fork-Join ペア。</summary>
    public required IReadOnlyList<ForkJoinPair> Pairs { get; init; }

    /// <summary>対応 Join が無い Fork。</summary>
    public required IReadOnlyList<string> UnpairedForks { get; init; }

    /// <summary>対応 Fork が無い Join。</summary>
    public required IReadOnlyList<string> UnpairedJoins { get; init; }

    /// <summary>同一 Join を非入れ子の複数 Fork が供給する組。</summary>
    public required IReadOnlyList<(string JoinState, IReadOnlyList<string> ForkStates)> AmbiguousJoins { get; init; }
}
