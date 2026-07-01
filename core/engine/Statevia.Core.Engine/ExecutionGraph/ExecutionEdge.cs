namespace Statevia.Core.Engine.ExecutionGraphs;

/// <summary>実行グラフの辺の種類。</summary>
public enum EdgeType
{
    /// <summary>単一の次ノードへの遷移。</summary>
    Next,

    /// <summary>Fork による分岐。</summary>
    Fork,

    /// <summary>Join への合流。</summary>
    Join,

    /// <summary>Wait からの再開。</summary>
    Resume,

    /// <summary>協調的キャンセル。</summary>
    Cancel
}

/// <summary>
/// 実行グラフの辺。あるノードから別ノードへの遷移を表します。
/// デバッグ・可視化用であり、実行には影響しません。
/// </summary>
public sealed class ExecutionEdge
{
    /// <summary>遷移元ノード ID。</summary>
    public required string From { get; init; }
    /// <summary>遷移先ノード ID。</summary>
    public required string To { get; init; }
    /// <summary>辺の種類。</summary>
    public required EdgeType Type { get; init; }
}
