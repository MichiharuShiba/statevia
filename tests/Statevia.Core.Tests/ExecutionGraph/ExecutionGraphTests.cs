using System.Linq;
using Statevia.Core.ExecutionGraphs;
using Xunit;

namespace Statevia.Core.Tests.ExecutionGraphs;

public class ExecutionGraphTests
{
    /// <summary>AddNode が空でないノード ID を返すことを検証する。</summary>
    [Fact]
    public void AddNode_ReturnsNodeId()
    {
        // Arrange
        var graph = new ExecutionGraph();

        // Act
        var id = graph.AddNode("Start");

        // Assert
        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    /// <summary>AddEdge がノード間の辺を記録し、Edges から取得できることを検証する。</summary>
    [Fact]
    public void AddEdge_RecordsRelationship()
    {
        // Arrange
        var graph = new ExecutionGraph();
        var id1 = graph.AddNode("A");
        var id2 = graph.AddNode("B");

        // Act
        graph.AddEdge(id1, id2, EdgeType.Next);
        var edges = graph.GetEdges();

        // Assert
        Assert.Single(edges);
        Assert.Equal(id1, edges[0].FromNodeId);
        Assert.Equal(id2, edges[0].ToNodeId);
        Assert.Equal(EdgeType.Next, edges[0].Type);
    }

    /// <summary>ExportJson が nodes と edges を含む JSON を返すことを検証する。</summary>
    [Fact]
    public void ExportJson_ReturnsValidJson()
    {
        // Arrange
        var graph = new ExecutionGraph();
        graph.AddNode("Start");

        // Act
        var json = graph.ExportJson();

        // Assert
        Assert.Contains("nodes", json, StringComparison.Ordinal);
        Assert.Contains("edges", json, StringComparison.Ordinal);
    }

    /// <summary>Nodes プロパティが追加したノードのスナップショットを返すことを検証する。</summary>
    [Fact]
    public void Nodes_ReturnsSnapshotOfAddedNodes()
    {
        // Arrange
        var graph = new ExecutionGraph();
        var id1 = graph.AddNode("A");
        var id2 = graph.AddNode("B");

        // Act
        var nodes = graph.GetNodes();

        // Assert
        Assert.Equal(2, nodes.Count);
        Assert.True(nodes.Any(n => n.NodeId == id1 && n.StateName == "A"));
        Assert.True(nodes.Any(n => n.NodeId == id2 && n.StateName == "B"));
    }
}
