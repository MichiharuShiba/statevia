namespace Statevia.Core.Api.Contracts;

/// <summary>契約 4.1 Graph Definition（UI 描画用の構造）。</summary>
public sealed class GraphDefinitionResponse
{
    public string GraphId { get; init; } = string.Empty;
    public IReadOnlyList<GraphNodeDefinition> Nodes { get; init; } = Array.Empty<GraphNodeDefinition>();
    public IReadOnlyList<GraphEdgeDefinition> Edges { get; init; } = Array.Empty<GraphEdgeDefinition>();
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class GraphNodeDefinition
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class GraphEdgeDefinition
{
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}
