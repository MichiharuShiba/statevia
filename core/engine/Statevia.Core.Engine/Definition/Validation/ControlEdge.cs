namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>定義上の制御辺の種別。</summary>
internal enum ControlEdgeKind
{
    /// <summary><c>on.Completed</c> 等の成功遷移（<c>next</c> / <c>fork</c> / <c>cases</c>）。</summary>
    Completed,

    /// <summary><c>on.Failed</c>（nodes の <c>error</c>）。供給には数えない。</summary>
    Failed,

    /// <summary>Wait <c>events</c>。</summary>
    WaitEvent,

    /// <summary>Wait <c>subscribe</c>。</summary>
    Subscribe
}

/// <summary>状態間の制御辺 1 本。</summary>
/// <param name="From">始点状態名。</param>
/// <param name="To">終点状態名。</param>
/// <param name="Kind">辺の種別。</param>
internal readonly record struct ControlEdge(string From, string To, ControlEdgeKind Kind)
{
    /// <summary>Join 供給の経路に含めるか（Failed は Join 供給に数えない）。</summary>
    public bool CountsAsSupply => Kind is not ControlEdgeKind.Failed;
}

/// <summary>Fork と対応 Join、各枝 Body。</summary>
/// <param name="ForkState">Fork 状態（Start）。</param>
/// <param name="JoinState">対応 Join（End）。</param>
/// <param name="Branches">枝先頭。</param>
/// <param name="BranchBodies">枝先頭 → Body 集合（互いに素。Join は含めない）。</param>
internal sealed record ForkJoinPair(
    string ForkState,
    string JoinState,
    IReadOnlyList<string> Branches,
    IReadOnlyDictionary<string, IReadOnlySet<string>> BranchBodies)
{
    /// <summary>全枝 Body の和（領域内ノード。Fork / Join 自身は含まない）。</summary>
    public IReadOnlySet<string> RegionBody { get; } = new HashSet<string>(
        BranchBodies.Values.SelectMany(static b => b),
        StringComparer.OrdinalIgnoreCase);
}

/// <summary>ノードが属する Fork-Join ペアと枝先頭。</summary>
/// <param name="Pair">所属ペア。</param>
/// <param name="BranchHead">枝先頭。</param>
internal readonly record struct BodyLocation(ForkJoinPair Pair, string BranchHead);
