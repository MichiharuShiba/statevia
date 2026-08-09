using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Xunit;

namespace Statevia.Core.Engine.Tests.ExecutionGraphs;

/// <summary><see cref="ExecutionGraph"/> の採番・辺・スナップショットを検証する。</summary>
public class ExecutionGraphTests
{
    private const string LowercaseHex12Pattern = "^[0-9a-f]{12}$";

    /// <summary>AddNode が小文字 Hex ちょうど 12 桁のノード ID を返すことを検証する。</summary>
    [Fact]
    public void AddNode_ReturnsLowercaseHex12NodeId()
    {
        // Arrange
        var graph = new ExecutionGraph();

        // Act
        var id = graph.AddNode("Start");

        // Assert
        Assert.Matches(LowercaseHex12Pattern, id);
    }

    /// <summary>候補が既存 NodeId と衝突したら再採番して一意な ID を返すことを検証する。</summary>
    [Fact]
    public void AddNode_WhenCandidateCollides_RetriesUntilUnique()
    {
        // Arrange
        var graph = new ExecutionGraph();
        const string colliding = "aaaaaaaaaaaa";
        const string unique = "bbbbbbbbbbbb";
        var callCount = 0;
        graph.SetNodeIdCandidateFactoryForTests(() =>
        {
            callCount++;
            return callCount == 1 ? colliding : unique;
        });
        Assert.Equal(colliding, graph.AddNode("Seed"));
        callCount = 0;
        graph.SetNodeIdCandidateFactoryForTests(() =>
        {
            callCount++;
            return callCount == 1 ? colliding : unique;
        });

        // Act
        var id = graph.AddNode("Next");

        // Assert
        Assert.Equal(unique, id);
        Assert.Equal(2, callCount);
        Assert.Equal(2, graph.GetNodesSnapshot().Count);
    }

    /// <summary>再採番が上限を超えたとき InvalidOperationException になることを検証する。</summary>
    [Fact]
    public void AddNode_WhenAllocationExhausted_ThrowsInvalidOperationException()
    {
        // Arrange
        var graph = new ExecutionGraph();
        const string colliding = "cccccccccccc";
        graph.SetNodeIdCandidateFactoryForTests(() => colliding);
        Assert.Equal(colliding, graph.AddNode("Seed"));

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => graph.AddNode("Next"));

        // Assert
        Assert.Contains("unique execution node ID", ex.Message, StringComparison.Ordinal);
        Assert.Single(graph.GetNodesSnapshot());
    }

    /// <summary>既存 8 桁 NodeId があるグラフでも新規は Hex 12 で追加できることを検証する。</summary>
    [Fact]
    public void AddNode_WhenGraphHasLegacyHex8Node_AllocatesHex12()
    {
        // Arrange
        var graph = new ExecutionGraph();
        graph.ImportFromCheckpoint(new CheckpointGraphData
        {
            Nodes =
            [
                new CheckpointGraphNode
                {
                    NodeId = "deadbeef",
                    StateName = "Legacy",
                    NodeType = "Task",
                    StartedAt = DateTime.UtcNow,
                    Attempt = 1
                }
            ],
            Edges = []
        });

        // Act
        var id = graph.AddNode("Fresh");

        // Assert
        Assert.Matches(LowercaseHex12Pattern, id);
        Assert.Equal(2, graph.GetNodesSnapshot().Count);
    }

    /// <summary>CompleteNode / SetNodeConditionRouting が NodeId を大文字小文字無視で解決することを検証する。</summary>
    [Fact]
    public void CompleteNode_AndSetConditionRouting_ResolveNodeIdIgnoreCase()
    {
        // Arrange
        var graph = new ExecutionGraph();
        graph.ImportFromCheckpoint(new CheckpointGraphData
        {
            Nodes =
            [
                new CheckpointGraphNode
                {
                    NodeId = "AaBbCcDdEeFf",
                    StateName = "Mixed",
                    NodeType = "Task",
                    StartedAt = DateTime.UtcNow,
                    Attempt = 1
                }
            ],
            Edges = []
        });
        var diagnostics = new ConditionRoutingDiagnostics
        {
            Fact = "Completed",
            Resolution = ConditionRoutingResolutions.Linear
        };

        // Act
        graph.SetNodeConditionRouting("aabbccddeeff", diagnostics);
        graph.CompleteNode("AABBCCDDEEFF", "Completed", new { ok = true });
        var node = Assert.Single(graph.GetNodesSnapshot());

        // Assert
        Assert.Equal("AaBbCcDdEeFf", node.NodeId);
        Assert.NotNull(node.CompletedAt);
        Assert.Equal("Completed", node.Fact);
        Assert.NotNull(node.ConditionRouting);
        Assert.Equal(ConditionRoutingResolutions.Linear, node.ConditionRouting.Resolution);
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
        var edges = graph.GetEdgesSnapshot();

        // Assert
        Assert.Single(edges);
        Assert.Equal(id1, edges[0].From);
        Assert.Equal(id2, edges[0].To);
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
        var nodes = graph.GetNodesSnapshot();

        // Assert
        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.NodeId == id1 && n.StateName == "A");
        Assert.Contains(nodes, n => n.NodeId == id2 && n.StateName == "B");
    }
}
