using System.Text.Json.Nodes;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.ExecutionGraphs;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>親 graph の論理 Fork/Join 合成（タスク 7 / D6）。</summary>
public sealed class PhysicalForkGraphComposerTests
{
    /// <summary>子ノードを親へ載せ、Fork 辺と workerId を引き継ぐ。</summary>
    [Fact]
    public void Compose_MergesChildNodesAndForkEdges_PreservingWorkerId()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {
                  "nodeId": "fork0001",
                  "nodeName": "ForkSrc",
                  "nodeType": "Task",
                  "startedAt": "2026-08-07T00:00:00Z",
                  "completedAt": "2026-08-07T00:00:01Z",
                  "fact": "Completed",
                  "attempt": 1,
                  "workerId": "parent-worker"
                },
                {
                  "nodeId": "join0001",
                  "nodeName": "Join1",
                  "nodeType": "Join",
                  "startedAt": "2026-08-07T00:00:02Z",
                  "completedAt": "2026-08-07T00:00:03Z",
                  "fact": "Joined",
                  "attempt": 1,
                  "workerId": "parent-worker"
                }
              ],
              "edges": []
            }
            """;
        const string childJson = """
            {
              "nodes": [
                {
                  "nodeId": "child001",
                  "nodeName": "A",
                  "nodeType": "Task",
                  "startedAt": "2026-08-07T00:00:01Z",
                  "completedAt": "2026-08-07T00:00:02Z",
                  "fact": "Completed",
                  "attempt": 2,
                  "workerId": "child-worker-a"
                }
              ],
              "edges": []
            }
            """;

        // Act
        var composed = PhysicalForkGraphComposer.Compose(
            parentJson,
            [new PhysicalForkGraphComposer.BranchGraph("fork0001", "join0001", "A", childJson)]);

        // Assert
        var root = JsonNode.Parse(composed)!.AsObject();
        var nodes = root["nodes"]!.AsArray();
        Assert.Equal(3, nodes.Count);

        var childNode = nodes.OfType<JsonObject>()
            .Single(n => n["nodeId"]!.GetValue<string>() == "child001");
        Assert.Equal("child-worker-a", childNode["workerId"]!.GetValue<string>());
        Assert.Equal(2, childNode["attempt"]!.GetValue<int>());

        var edges = root["edges"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "fork0001"
                && e["to"]!.GetValue<string>() == "child001"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Fork);
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "child001"
                && e["to"]!.GetValue<string>() == "join0001"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Join);
    }

    /// <summary>
    /// ネスト時、親 Fork は枝先頭のみへ Fork 辺、内側 Join→外側 Join は Next（Join 辺にしない）。
    /// </summary>
    [Fact]
    public void Compose_WhenNestedFork_DoesNotAddExtraJoinInletsBeyondBranchHeads()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {"nodeId":"outer-fork","nodeName":"OuterFork","nodeType":"Fork"},
                {"nodeId":"outer-join","nodeName":"OuterJoin","nodeType":"Join"}
              ],
              "edges": []
            }
            """;
        // 内側合成済み: OuterFork 枝先頭 InnerFork + InnerA/B + InnerJoin
        const string nestedChildJson = """
            {
              "nodes": [
                {"nodeId":"inner-fork","nodeName":"InnerFork","nodeType":"Fork"},
                {"nodeId":"inner-a","nodeName":"InnerA","nodeType":"Task"},
                {"nodeId":"inner-b","nodeName":"InnerB","nodeType":"Task"},
                {"nodeId":"inner-join","nodeName":"InnerJoin","nodeType":"Join"}
              ],
              "edges": [
                {"from":"inner-fork","to":"inner-a","type":1},
                {"from":"inner-fork","to":"inner-b","type":1},
                {"from":"inner-a","to":"inner-join","type":2},
                {"from":"inner-b","to":"inner-join","type":2}
              ]
            }
            """;
        const string fastChildJson = """
            {"nodes":[{"nodeId":"outer-fast","nodeName":"OuterFast","nodeType":"Task"}],"edges":[]}
            """;

        // Act
        var composed = PhysicalForkGraphComposer.Compose(
            parentJson,
            [
                new PhysicalForkGraphComposer.BranchGraph(
                    "outer-fork", "outer-join", "OuterFast", fastChildJson),
                new PhysicalForkGraphComposer.BranchGraph(
                    "outer-fork", "outer-join", "InnerFork", nestedChildJson)
            ]);

        // Assert
        var edges = JsonNode.Parse(composed)!["edges"]!.AsArray().OfType<JsonObject>().ToList();

        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "outer-fork"
                && e["to"]!.GetValue<string>() == "outer-fast"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Fork);
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "outer-fork"
                && e["to"]!.GetValue<string>() == "inner-fork"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Fork);
        // 定義にない「OuterFork → InnerA」は付けない
        Assert.DoesNotContain(
            edges,
            e => e["from"]!.GetValue<string>() == "outer-fork"
                && e["to"]!.GetValue<string>() == "inner-a");
        Assert.DoesNotContain(
            edges,
            e => e["from"]!.GetValue<string>() == "outer-fork"
                && e["to"]!.GetValue<string>() == "inner-b");

        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "outer-fast"
                && e["to"]!.GetValue<string>() == "outer-join"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Join);
        // 内側 Join → 外側 Join は Next（余分な Join 合流枝に見せない）
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "inner-join"
                && e["to"]!.GetValue<string>() == "outer-join"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Next);
        Assert.DoesNotContain(
            edges,
            e => e["from"]!.GetValue<string>() == "inner-join"
                && e["to"]!.GetValue<string>() == "outer-join"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Join);
        Assert.DoesNotContain(
            edges,
            e => e["from"]!.GetValue<string>() == "inner-a"
                && e["to"]!.GetValue<string>() == "outer-join");
    }

    /// <summary>循環で同名 Join が複数でも、各 Fork 訪問の Join nodeId に辺が付く。</summary>
    [Fact]
    public void Compose_WhenCyclicForkJoin_AttachesJoinEdgesToVisitSpecificNodeIds()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {
                  "nodeId": "fork-v1",
                  "nodeName": "Fork1",
                  "nodeType": "Fork",
                  "startedAt": "2026-08-07T00:00:00Z",
                  "completedAt": "2026-08-07T00:00:01Z"
                },
                {
                  "nodeId": "join-v1",
                  "nodeName": "Join1",
                  "nodeType": "Join",
                  "startedAt": "2026-08-07T00:00:10Z",
                  "completedAt": "2026-08-07T00:00:11Z"
                },
                {
                  "nodeId": "task2-v1",
                  "nodeName": "Task2",
                  "nodeType": "Task",
                  "startedAt": "2026-08-07T00:00:12Z",
                  "completedAt": "2026-08-07T00:00:13Z"
                },
                {
                  "nodeId": "fork-v2",
                  "nodeName": "Fork1",
                  "nodeType": "Fork",
                  "startedAt": "2026-08-07T00:00:14Z",
                  "completedAt": "2026-08-07T00:00:15Z"
                },
                {
                  "nodeId": "join-v2",
                  "nodeName": "Join1",
                  "nodeType": "Join",
                  "startedAt": "2026-08-07T00:00:20Z",
                  "completedAt": "2026-08-07T00:00:21Z"
                }
              ],
              "edges": []
            }
            """;
        const string childV1 = """
            {"nodes":[{"nodeId":"task1-v1","nodeName":"Task1","nodeType":"Task"}],"edges":[]}
            """;
        const string childV2 = """
            {"nodes":[{"nodeId":"task1-v2","nodeName":"Task1","nodeType":"Task"}],"edges":[]}
            """;

        var joinV1 = PhysicalForkGraphComposer.ResolveJoinNodeId(parentJson, "fork-v1", "Join1");
        var joinV2 = PhysicalForkGraphComposer.ResolveJoinNodeId(parentJson, "fork-v2", "Join1");

        // Act
        var composed = PhysicalForkGraphComposer.Compose(
            parentJson,
            [
                new PhysicalForkGraphComposer.BranchGraph("fork-v1", joinV1!, "Task1", childV1),
                new PhysicalForkGraphComposer.BranchGraph("fork-v2", joinV2!, "Task1", childV2)
            ]);

        // Assert
        Assert.Equal("join-v1", joinV1);
        Assert.Equal("join-v2", joinV2);

        var edges = JsonNode.Parse(composed)!["edges"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "task1-v1"
                && e["to"]!.GetValue<string>() == "join-v1"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Join);
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "task1-v2"
                && e["to"]!.GetValue<string>() == "join-v2"
                && e["type"]!.GetValue<int>() == (int)EdgeType.Join);
        Assert.DoesNotContain(
            edges,
            e => e["from"]!.GetValue<string>() == "task1-v2"
                && e["to"]!.GetValue<string>() == "join-v1");
    }

    /// <summary>JoinNodeId が空のとき Join 辺を張らない。</summary>
    [Fact]
    public void Compose_WhenJoinNodeIdEmpty_SkipsJoinEdges()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {"nodeId":"fork0001","nodeName":"Fork1"},
                {"nodeId":"join0001","nodeName":"Join1","nodeType":"Join"}
              ],
              "edges": []
            }
            """;
        const string childJson = """
            {"nodes":[{"nodeId":"child001","nodeName":"A"}],"edges":[]}
            """;

        // Act
        var composed = PhysicalForkGraphComposer.Compose(
            parentJson,
            [new PhysicalForkGraphComposer.BranchGraph("fork0001", "", "A", childJson)]);

        // Assert
        var edges = JsonNode.Parse(composed)!["edges"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(
            edges,
            e => e["from"]!.GetValue<string>() == "fork0001"
                && e["to"]!.GetValue<string>() == "child001");
        Assert.DoesNotContain(edges, e => e["type"]!.GetValue<int>() == (int)EdgeType.Join);
    }

    /// <summary>分岐が空のとき親 JSON をそのまま返す。</summary>
    [Fact]
    public void Compose_WhenNoBranches_ReturnsParentUnchanged()
    {
        // Arrange
        const string parentJson = """{"nodes":[{"nodeId":"a"}],"edges":[]}""";

        // Act
        var composed = PhysicalForkGraphComposer.Compose(parentJson, []);

        // Assert
        Assert.Equal(parentJson, composed);
    }

    /// <summary>不正な親 JSON は合成せずそのまま返す。</summary>
    [Fact]
    public void Compose_WhenParentJsonInvalid_ReturnsParentUnchanged()
    {
        // Arrange
        const string parentJson = "not-json";

        // Act
        var composed = PhysicalForkGraphComposer.Compose(
            parentJson,
            [new PhysicalForkGraphComposer.BranchGraph("f", "j", "A", """{"nodes":[],"edges":[]}""")]);

        // Assert
        Assert.Equal(parentJson, composed);
    }

    /// <summary>Fork に時刻が無いとき visit index で Join を解決する。</summary>
    [Fact]
    public void ResolveJoinNodeId_WhenForkHasNoTime_UsesVisitIndex()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {"nodeId":"fork-a","nodeName":"ForkSame"},
                {"nodeId":"fork-b","nodeName":"ForkSame"},
                {"nodeId":"join-a","nodeName":"JoinSame","nodeType":"Join","startedAt":"2026-01-01T00:00:00Z"},
                {"nodeId":"join-b","nodeName":"JoinSame","nodeType":"Join","startedAt":"2026-01-01T00:00:01Z"}
              ],
              "edges": []
            }
            """;

        // Act
        var joinForA = PhysicalForkGraphComposer.ResolveJoinNodeId(parentJson, "fork-a", "JoinSame");
        var joinForB = PhysicalForkGraphComposer.ResolveJoinNodeId(parentJson, "fork-b", "JoinSame");

        // Assert
        Assert.Equal("join-a", joinForA);
        Assert.Equal("join-b", joinForB);
    }

    /// <summary>joinState が空白なら null。</summary>
    [Fact]
    public void ResolveJoinNodeId_WhenJoinStateBlank_ReturnsNull()
    {
        // Act
        var join = PhysicalForkGraphComposer.ResolveJoinNodeId(
            """{"nodes":[{"nodeId":"f"}],"edges":[]}""",
            "f",
            "  ");

        // Assert
        Assert.Null(join);
    }

    /// <summary>Fork の nodeName が空なら先頭 Join 候補を返す。</summary>
    [Fact]
    public void ResolveJoinNodeId_WhenForkNameMissing_ReturnsFirstJoinCandidate()
    {
        // Arrange
        const string parentJson = """
            {
              "nodes": [
                {"nodeId":"fork1"},
                {"nodeId":"join1","nodeName":"J","nodeType":"Join","startedAt":"2026-01-01T00:00:00Z"},
                {"nodeId":"join2","nodeName":"J","nodeType":"Join","startedAt":"2026-01-01T00:00:01Z"}
              ],
              "edges": []
            }
            """;

        // Act
        var join = PhysicalForkGraphComposer.ResolveJoinNodeId(parentJson, "fork1", "J");

        // Assert
        Assert.Equal("join1", join);
    }
}
