using System.Collections.Generic;

namespace Statevia.Service.Api.Contracts;

/// <summary>契約 4.1 Graph Definition（UI 描画用の構造）。</summary>
public sealed class GraphDefinitionResponse
{
    /// <summary>グラフ ID（定義の display 等）。</summary>
    public string GraphId { get; init; } = string.Empty;

    /// <summary>ノード定義の列。</summary>
    public IReadOnlyList<GraphNodeDefinition> Nodes { get; init; } = Array.Empty<GraphNodeDefinition>();

    /// <summary>辺定義の列。</summary>
    public IReadOnlyList<GraphEdgeDefinition> Edges { get; init; } = Array.Empty<GraphEdgeDefinition>();
}

/// <summary>グラフ上の 1 ノード。</summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class GraphNodeDefinition
{
    /// <summary>ノード ID（キャンバス上のキー）。</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>ノード種別。</summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>表示ラベル。</summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>グラフ上の 1 辺。</summary>
public sealed class GraphEdgeDefinition
{
    /// <summary>始点ノード ID。</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>終点ノード ID。</summary>
    public string To { get; init; } = string.Empty;
}
