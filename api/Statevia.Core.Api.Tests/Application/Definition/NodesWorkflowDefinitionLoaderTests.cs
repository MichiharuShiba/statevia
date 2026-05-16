using Statevia.Core.Api.Application.Definition;

namespace Statevia.Core.Api.Tests.Application.Definition;

/// <summary><see cref="NodesWorkflowDefinitionLoader"/> の異常系・境界の単体テスト。</summary>
public sealed class NodesWorkflowDefinitionLoaderTests
{
    private readonly NodesWorkflowDefinitionLoader _loader = new();

    /// <summary>ルート controls は MVP 非対応として拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenRootControlsPresent()
    {
        var yaml = """
            version: 1
            workflow:
              name: N
            controls: []
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        Assert.Contains("controls", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes 配列が空のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenNodesArrayEmpty()
    {
        var yaml = """
            version: 1
            workflow:
              name: N
            nodes: []
            """;

        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        Assert.Contains("nodes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes 配列に null 要素があるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenNodesContainsNullEntry()
    {
        var yaml = """
            version: 1
            workflow:
              name: N
            nodes:
              - id: start
                type: start
                next: endNode
              -
              - id: endNode
                type: end
            """;

        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
