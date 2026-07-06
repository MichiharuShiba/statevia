using Statevia.Service.Api.Application.Definition;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.FSM;

namespace Statevia.Service.Api.Tests.Application.Definition;

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
        Assert.Equal("Minimal", definition.Name);
        Assert.True(definition.States.ContainsKey("start"));
        Assert.True(definition.States.ContainsKey("endNode"));
        Assert.True(definition.States["endNode"].On![Fact.Completed].End);
    }

    /// <summary>fork / join（mode: all）から Join.All が構築される。</summary>
    [Fact]
    public void Load_ForkJoin_BuildsJoinAll()
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
        Assert.Equal(2, join.All.Count);
        Assert.Contains("b1", join.All, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("b2", join.All, StringComparer.OrdinalIgnoreCase);
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
        Assert.Equal("wf-from-id", definition.Name);
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

    /// <summary>root version 欠落時は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenVersionMissing()
    {
        // Arrange
        var yaml = """
            workflow:
              name: N
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

    /// <summary>workflow 欠落時は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWorkflowMissing()
    {
        // Arrange
        var yaml = """
            version: 1
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
        Assert.Contains("workflow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>start が複数あるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenMultipleStartNodes()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: TwoStarts
            nodes:
              - id: start1
                type: start
                next: endNode
              - id: start2
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>join に対応する fork がないとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenJoinHasNoMatchingFork()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: OrphanJoin
            nodes:
              - id: start
                type: start
                next: join1
              - id: join1
                type: join
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("no matching fork", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>fork に edges があるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenForkHasEdges()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ForkEdges
            nodes:
              - id: start
                type: start
                next: fork1
              - id: fork1
                type: fork
                branches: [b1, b2]
                edges:
                  - to: b1
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
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("edges", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>action ノードに action 属性がないとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenActionMissingActionId()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: NoAction
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'action'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>edge.to の object 形式（id）を受理する。</summary>
    [Fact]
    public void Load_EdgeToObject_Succeeds()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: EdgeObj
            nodes:
              - id: start
                type: start
                edges:
                  - to:
                      id: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("endNode", definition.States["start"].On![Fact.Completed].Next);
    }

    /// <summary>複数 fork が同一 join に合致するとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenMultipleForksMatchJoin()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: MultiFork
            nodes:
              - id: start
                type: start
                next: forkOuter
              - id: forkOuter
                type: fork
                branches: [fork1, fork2]
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
              - id: fork2
                type: fork
                branches: [c1, c2]
              - id: c1
                type: action
                action: noop
                next: join1
              - id: c2
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
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("multiple forks", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>end ノードが無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenEndNodeMissing()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: NoEnd
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("type: end", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>action.error が未知ノードを指すとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenActionErrorReferencesUnknownNode()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: BadErrorRef
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                error: missing
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("references unknown id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>start に outgoing が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenStartHasNoOutgoing()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: StartNoOut
            nodes:
              - id: start
                type: start
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'next' or 'edges'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>action に outgoing が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenActionHasNoOutgoing()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ActionNoOut
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'next' or 'edges'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait に event が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitMissingEvent()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitNoEvent
            nodes:
              - id: start
                type: start
                next: wait1
              - id: wait1
                type: wait
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'event'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>fork の branches が 1 件のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenForkHasSingleBranch()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: ForkOneBranch
            nodes:
              - id: start
                type: start
                next: fork1
              - id: fork1
                type: fork
                branches: [b1]
              - id: b1
                type: action
                action: noop
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("at least 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>join に outgoing が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenJoinHasNoOutgoing()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: JoinNoOut
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
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'next' or 'edges'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>start の edges が未知 ID を指すとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenStartEdgeReferencesUnknown()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: StartBadEdge
            nodes:
              - id: start
                type: start
                edges:
                  - to: missing
              - id: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("references unknown id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>条件付き edges に default が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenConditionalEdgesMissingDefault()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: NoDefaultEdge
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
                  - to: low
                    when:
                      path: $.x
                      op: lte
                      value: 0
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
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("default/unconditional edge", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>無条件 edges が複数あるとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenMultipleUnconditionalEdges()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: MultiUncondEdge
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                edges:
                  - to: b
                  - to: endNode
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
        Assert.Contains("exactly one edge is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>workflow.modules を WorkflowDefinition.Modules に保持する。</summary>
    [Fact]
    public void Load_ParsesWorkflowModules()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: W
              modules:
                mail: com.company.mail
            nodes:
              - id: start
                type: start
                next: act
              - id: act
                type: action
                action: mail.send
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.NotNull(definition.Modules);
        Assert.Single(definition.Modules!);
        Assert.Equal("com.company.mail", definition.Modules!["mail"].ModuleId);
        Assert.Equal("mail.send", definition.States["act"].Action);
    }

    /// <summary>action ノード直下の retry を StateDefinition.Retry に引き継ぐ。</summary>
    [Fact]
    public void Load_ActionNodeRetry_PreservedOnState()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: W
            nodes:
              - id: start
                type: start
                next: act
              - id: act
                type: action
                action: noop
                retry:
                  limit: 2
                  backoff: exponential
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);
        var retry = definition.States["act"].Retry;

        // Assert
        Assert.NotNull(retry);
        Assert.Equal(2, retry!.Limit);
        Assert.Equal("exponential", retry.Backoff);
    }

    /// <summary>空の retry ブロックは nodes→states 変換後 Retry=null になる。</summary>
    [Fact]
    public void Load_ActionNodeEmptyRetry_ReturnsNullRetry()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: W
            nodes:
              - id: start
                type: start
                next: act
              - id: act
                type: action
                action: noop
                retry: {}
                next: endNode
              - id: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Null(definition.States["act"].Retry);
    }

    /// <summary>空の workflow.modules は Modules=null として扱う。</summary>
    [Fact]
    public void Load_EmptyWorkflowModules_ReturnsNullModules()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: W
              modules: {}
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
        Assert.Null(definition.Modules);
    }

    /// <summary>input 内の retry は構文エラーになる。</summary>
    [Fact]
    public void Load_RetryInsideInput_Throws()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: W
            nodes:
              - id: start
                type: start
                next: act
              - id: act
                type: action
                action: noop
                input:
                  retry:
                    limit: 3
                next: endNode
              - id: endNode
                type: end
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        Assert.Contains("retry", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
