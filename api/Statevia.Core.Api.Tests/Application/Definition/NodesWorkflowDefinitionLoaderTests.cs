using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Api.Tests.Application.Definition;

/// <summary><see cref="NodesWorkflowDefinitionLoader"/> の nodes 形式 YAML 読み込みテスト。</summary>
public sealed class NodesWorkflowDefinitionLoaderTests
{
    private readonly NodesWorkflowDefinitionLoader _loader = new();

    /// <summary>最小の start → end 定義を読み込める。</summary>
    [Fact]
    public void Load_MinimalStartEnd_Succeeds()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: Minimal
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("Minimal", definition.Workflow.Name);
        Assert.True(definition.States.ContainsKey("start"));
        Assert.True(definition.States.ContainsKey("endNode"));
        Assert.True(definition.States["endNode"].On![Fact.Completed].End);
    }

    /// <summary>fork / join（mode: all）から Join.AllOf が構築される。</summary>
    [Fact]
    public void Load_ForkJoin_BuildsJoinAllOf()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ForkJoin
            nodes:
              - id: start
                type: start
                next: fork1
              - id: fork1
                type: fork
                branches: [b1, b2]
              - id: b1
                type: action
                action: noop
                next: join1
              - id: b2
                type: action
                action: noop
                next: join1
              - id: join1
                type: join
                mode: all
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        var join = definition.States["join1"].Join;
        Assert.NotNull(join);
        Assert.Equal(2, join.AllOf.Count);
        Assert.Contains("b1", join.AllOf, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("b2", join.AllOf, StringComparer.OrdinalIgnoreCase);
        Assert.True(definition.States["join1"].On!.ContainsKey(Fact.Joined));
    }

    /// <summary>action.error は on.Failed 遷移として正規化される。</summary>
    [Fact]
    public void Load_ActionError_AddsOnFailedTransition()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: FailedPath
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                error: failedHandler
              - id: failedHandler
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("failedHandler", definition.States["a"].On![Fact.Failed].Next);
    }

    /// <summary>action.error の object 形式（id）を受理する。</summary>
    [Fact]
    public void Load_ActionErrorObject_NormalizesTarget()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: FailedPathObj
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                error:
                  id: failedHandler
              - id: failedHandler
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("failedHandler", definition.States["a"].On![Fact.Failed].Next);
    }

    /// <summary>wait ノードに Wait.Event が設定される。</summary>
    [Fact]
    public void Load_WaitNode_SetsWaitEvent()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitFlow
            nodes:
              - id: start
                type: start
                next: wait1
              - id: wait1
                type: wait
                event: resume
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("resume", definition.States["wait1"].Wait!.Event);
    }

    /// <summary>条件付き edges は Completed 遷移の cases/default に正規化される。</summary>
    [Fact]
    public void Load_ConditionalEdges_BuildsCasesAndDefault()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ConditionalEdges
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                edges:
                  - to: high
                    when:
                      path: $.x
                      op: gt
                      value: 0
                    order: 10
                  - to: low
                    when:
                      path: $.x
                      op: lte
                      value: 0
                    order: 20
                  - to: low
              - id: high
                type: action
                action: noop
                next: endNode
              - id: low
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        var onCompleted = definition.States["a"].On![Fact.Completed];
        Assert.NotNull(onCompleted.Cases);
        Assert.Equal(2, onCompleted.Cases!.Count);
        Assert.NotNull(onCompleted.Default);
        Assert.Equal("low", onCompleted.Default!.Next);
    }

    /// <summary>start が edges のみでも読み込める。</summary>
    [Fact]
    public void Load_StartWithEdgesOnly_Succeeds()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: StartEdgesOnly
            nodes:
              - id: start
                type: start
                edges:
                  - to: a
              - id: a
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.True(definition.States.ContainsKey("start"));
        Assert.Equal("a", definition.States["start"].On![Fact.Completed].Next);
    }

    /// <summary>workflow.name 未指定時は workflow.id を名前に使う。</summary>
    [Fact]
    public void Load_WorkflowName_FallsBackToWorkflowId()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              id: wf-from-id
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("wf-from-id", definition.Workflow.Name);
    }

    /// <summary>ルート controls は MVP 非対応として拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenRootControlsPresent()
    {
        // Arrange
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

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("controls", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes 配列が空のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenNodesArrayEmpty()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: N
            nodes: []
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("nodes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>nodes 配列に null 要素があるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenNodesContainsNullEntry()
    {
        // Arrange
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

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>start から到達不能なノードがあるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenUnreachableNode()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: Unreachable
            nodes:
              - id: start
                type: start
                next: endNode
              - id: orphan
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Unreachable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orphan", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>ノード ID が重複しているとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenDuplicateNodeId()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: Dup
            nodes:
              - id: start
                type: start
                next: endNode
              - id: Start
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>action.error の自己参照は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenActionErrorSelfReference()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ErrorSelf
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                error: a
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("self-reference", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait ノードの error 属性は MVP 非対応として拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitHasErrorKey()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ErrorOnWait
            nodes:
              - id: start
                type: start
                next: wait1
              - id: wait1
                type: wait
                event: resume
                next: endNode
                error: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>next と単一無条件 edges の遷移先が不一致のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenNextAndUnconditionalEdgeMismatch()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: EdgeNextMismatch
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                edges:
                  - to: b
              - id: b
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>join の mode が all 以外のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenJoinModeIsNotAll()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: JoinMode
            nodes:
              - id: start
                type: start
                next: fork1
              - id: fork1
                type: fork
                branches: [b1, b2]
              - id: b1
                type: action
                action: noop
                next: join1
              - id: b2
                type: action
                action: noop
                next: join1
              - id: join1
                type: join
                mode: any
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("mode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>未知の node type は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenUnknownNodeType()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: BadType
            nodes:
              - id: start
                type: mystery
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Unknown node type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>end ノードに next があるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenEndNodeHasNext()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: EndNext
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
                next: start
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("type: end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>version が 1 以外のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenVersionIsNotOne()
    {
        // Arrange
        var yaml = """
            version: 2
            workflow:
              name: BadVersion
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
