namespace Statevia.Core.ExecutionGraphs;

/// <summary>実行グラフの辺の種類。</summary>
public enum EdgeType { Next, Fork, Join, Resume, Cancel }

/// <summary>
/// 実行グラフの辺。あるノードから別ノードへの遷移を表します。
/// デバッグ・可視化用であり、実行には影響しません。
/// </summary>
public sealed class ExecutionEdge
{
    /// <summary>遷移元ノード ID。</summary>
    public required string FromNodeId { get; init; }
    /// <summary>遷移先ノード ID。</summary>
    public required string ToNodeId { get; init; }
    /// <summary>辺の種類。</summary>
    public required EdgeType Type { get; init; }
}
