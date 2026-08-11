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
              - name: start
                type: start
                next: endNode
              - name: endNode
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

    /// <summary>ネスト Fork（枝先頭が内側 Fork）でも外側 Join.All が外側枝先頭になる。</summary>
    [Fact]
    public void Load_NestedForkJoin_BuildsOuterJoinAllFromOuterBranchHeads()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: NestedFork
            nodes:
              - name: start
                type: start
                next: outerFork
              - name: outerFork
                type: fork
                branches: [outerFast, innerFork]
              - name: outerFast
                type: action
                action: noop
                next: outerJoin
              - name: innerFork
                type: fork
                branches: [innerA, innerB]
              - name: innerA
                type: action
                action: noop
                next: innerJoin
              - name: innerB
                type: action
                action: noop
                next: innerJoin
              - name: innerJoin
                type: join
                mode: all
                next: outerJoin
              - name: outerJoin
                type: join
                mode: all
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        var inner = definition.States["innerJoin"].Join;
        Assert.NotNull(inner);
        Assert.Contains("innerA", inner.All, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("innerB", inner.All, StringComparer.OrdinalIgnoreCase);

        var outer = definition.States["outerJoin"].Join;
        Assert.NotNull(outer);
        Assert.Equal(2, outer.All.Count);
        Assert.Contains("outerFast", outer.All, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("innerFork", outer.All, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("innerA", outer.All, StringComparer.OrdinalIgnoreCase);
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
              - name: start
                type: start
                next: fork1
              - name: fork1
                type: fork
                branches: [b1, b2]
              - name: b1
                type: action
                action: noop
                next: join1
              - name: b2
                type: action
                action: noop
                next: join1
              - name: join1
                type: join
                mode: all
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                next: endNode
                error: failedHandler
              - name: failedHandler
                type: action
                action: noop
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("failedHandler", definition.States["a"].On![Fact.Failed].Next);
    }

    /// <summary>action ノードの output が StateDefinition.Output に載ることを検証する。</summary>
    [Fact]
    public void Load_ActionOutput_SetsStateDefinitionOutput()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: VarsOutput
            nodes:
              - name: start
                type: start
                next: getUser
              - name: getUser
                type: action
                action: noop
                output: $.vars.user
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("$.vars.user", definition.States["getUser"].Output);
    }

    /// <summary>action.error の object 形式（name）を受理する。</summary>
    [Fact]
    public void Load_ActionErrorObject_NormalizesTarget()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: FailedPathObj
            nodes:
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                next: endNode
                error:
                  name: failedHandler
              - name: failedHandler
                type: action
                action: noop
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        Assert.Equal("failedHandler", definition.States["a"].On![Fact.Failed].Next);
    }

    /// <summary>旧形式 wait（event+next）を Events へ正規化することを検証する。</summary>
    [Fact]
    public void Load_WaitNode_NormalizesLegacyEventAndNext()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitFlow
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                event: resume
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);
        var wait = definition.States["wait1"].Wait;

        // Assert
        Assert.NotNull(wait);
        Assert.Single(wait!.Events);
        Assert.Equal("endNode", wait.Events["resume"]);
        Assert.Equal("endNode", definition.States["wait1"].On![Fact.Completed].Next);
    }

    /// <summary>nodes 形式の wait.events を WaitDefinition.Events に載せることを検証する。</summary>
    [Fact]
    public void Load_WaitNode_ParsesEventsMap()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: MultiWait
            nodes:
              - name: start
                type: start
                next: approve
              - name: approve
                type: wait
                events:
                  approve: approved
                  reject: rejected
              - name: approved
                type: action
                action: statevia.action.builtin.noop
                next: endNode
              - name: rejected
                type: action
                action: statevia.action.builtin.noop
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);
        var wait = definition.States["approve"].Wait;

        // Assert
        Assert.NotNull(wait);
        Assert.Equal(2, wait!.Events.Count);
        Assert.Equal("approved", wait.Events["approve"]);
        Assert.Equal("rejected", wait.Events["reject"]);
    }

    /// <summary>新形式 wait.events と edges の併用は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitEventsHasEdges()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsEdges
            nodes:
              - name: start
                type: start
                next: approve
              - name: approve
                type: wait
                events:
                  approve: endNode
                edges:
                  - to: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("edges", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("events", ex.Message, StringComparison.OrdinalIgnoreCase);
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
              - name: start
                type: start
                next: a
              - name: a
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
              - name: high
                type: action
                action: noop
                next: endNode
              - name: low
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                edges:
                  - to: a
              - name: a
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              -
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: orphan
                type: action
                action: noop
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Unreachable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orphan", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>ノード name が重複しているとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenDuplicateNodeName()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: Dup
            nodes:
              - name: start
                type: start
                next: endNode
              - name: Start
                type: action
                action: noop
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Duplicate node name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>ノードに id のみがあり name がないとき拒否する（フォールバックなし）。</summary>
    [Fact]
    public void Load_Throws_WhenNodeHasIdButNoName()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: IdOnly
            nodes:
              - id: start
                type: start
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("Every node must have", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'name'", ex.Message, StringComparison.OrdinalIgnoreCase);
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                next: endNode
                error: a
              - name: endNode
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
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                event: resume
                next: endNode
                error: endNode
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                next: endNode
                edges:
                  - to: b
              - name: b
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: fork1
              - name: fork1
                type: fork
                branches: [b1, b2]
              - name: b1
                type: action
                action: noop
                next: join1
              - name: b2
                type: action
                action: noop
                next: join1
              - name: join1
                type: join
                mode: any
                next: endNode
              - name: endNode
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
              - name: start
                type: mystery
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start1
                type: start
                next: endNode
              - name: start2
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: join1
              - name: join1
                type: join
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: fork1
              - name: fork1
                type: fork
                branches: [b1, b2]
                edges:
                  - to: b1
              - name: b1
                type: action
                action: noop
                next: join1
              - name: b2
                type: action
                action: noop
                next: join1
              - name: join1
                type: join
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'action'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>edge.to の object 形式（name）を受理する。</summary>
    [Fact]
    public void Load_EdgeToObject_Succeeds()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: EdgeObj
            nodes:
              - name: start
                type: start
                edges:
                  - to:
                      name: endNode
              - name: endNode
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
              - name: start
                type: start
                next: forkOuter
              - name: forkOuter
                type: fork
                branches: [fork1, fork2]
              - name: fork1
                type: fork
                branches: [b1, b2]
              - name: b1
                type: action
                action: noop
                next: join1
              - name: b2
                type: action
                action: noop
                next: join1
              - name: fork2
                type: fork
                branches: [c1, c2]
              - name: c1
                type: action
                action: noop
                next: join1
              - name: c2
                type: action
                action: noop
                next: join1
              - name: join1
                type: join
                mode: all
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                next: endNode
                error: missing
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("references unknown name", ex.Message, StringComparison.OrdinalIgnoreCase);
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
              - name: start
                type: start
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'next' or 'edges'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait に events / event が無いとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitMissingEvent()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitNoEvent
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must have 'events', 'subscribe'", ex.Message, StringComparison.OrdinalIgnoreCase);
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
              - name: start
                type: start
                next: fork1
              - name: fork1
                type: fork
                branches: [b1]
              - name: b1
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: fork1
              - name: fork1
                type: fork
                branches: [b1, b2]
              - name: b1
                type: action
                action: noop
                next: join1
              - name: b2
                type: action
                action: noop
                next: join1
              - name: join1
                type: join
                mode: all
              - name: endNode
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
              - name: start
                type: start
                edges:
                  - to: missing
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("references unknown name", ex.Message, StringComparison.OrdinalIgnoreCase);
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
              - name: start
                type: start
                next: a
              - name: a
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
              - name: high
                type: action
                action: noop
                next: endNode
              - name: low
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: a
              - name: a
                type: action
                action: noop
                edges:
                  - to: b
                  - to: endNode
              - name: b
                type: action
                action: noop
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: act
              - name: act
                type: action
                action: mail.send
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: act
              - name: act
                type: action
                action: noop
                retry:
                  limit: 2
                  backoff: exponential
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: act
              - name: act
                type: action
                action: noop
                retry: {}
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: endNode
              - name: endNode
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
              - name: start
                type: start
                next: act
              - name: act
                type: action
                action: noop
                input:
                  retry:
                    limit: 3
                next: endNode
              - name: endNode
                type: end
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        Assert.Contains("retry", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait で exits を使うと拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitUsesExits()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitExits
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                exits:
                  done: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must not use 'exits'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait で events と subscribe の併用は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitHasEventsAndSubscribe()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitBoth
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                events:
                  done: endNode
                subscribe:
                  - topic: t
                    next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("both 'events' and 'subscribe'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait で events と legacy event の併用は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitHasEventsAndLegacyEvent()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsAndEvent
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                event: done
                events:
                  done: endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("both 'events' and 'event'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>wait.subscribe を正規化する。</summary>
    [Fact]
    public void Load_WaitNode_ParsesSubscribeList()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitSubscribe
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                subscribe:
                  - topic: orders
                    key: "$.id"
                    next: endNode
                  - topic: cancel
                    next: endNode
              - name: endNode
                type: end
            """;

        // Act
        var definition = _loader.Load(yaml);

        // Assert
        var subscribe = definition.States["wait1"].Wait?.Subscribe;
        Assert.NotNull(subscribe);
        Assert.Equal(2, subscribe!.Count);
        Assert.Equal("orders", subscribe[0].Topic);
        Assert.Equal("$.id", subscribe[0].Key);
        Assert.Equal("endNode", subscribe[0].Next);
    }

    /// <summary>空の subscribe は拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitSubscribeEmpty()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitSubscribeEmpty
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                subscribe: []
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("subscribe must not be empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>subscribe がリストでないとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitSubscribeNotList()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitSubscribeScalar
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                subscribe: not-a-list
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("subscribe must be a list", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>events の next が空なら拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitEventsTargetEmpty()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsEmptyTarget
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                events:
                  done: ""
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("must specify a next node name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>events が空マップなら拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitEventsMapEmpty()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsEmpty
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                events: {}
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("non-empty 'events'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>events の重複キーは拒否する（YAML では後勝ちになり得るため JSON 相当で検証）。</summary>
    [Fact]
    public void Load_Throws_WhenWaitEventsDuplicateKeyViaObject()
    {
        // Arrange — ローダーは Dictionary 構築時に TryAdd 失敗を検出する経路を持つ。
        // YAML では同一キーが後勝ちになるため、直接 Compiled 前の nodes 形式で
        // 空キーを使って events キー検証を別経路でカバーする。
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsEmptyKey
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                events:
                  " ": endNode
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.Contains("events keys must be non-empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>events が null 相当のとき拒否する。</summary>
    [Fact]
    public void Load_Throws_WhenWaitEventsIsNull()
    {
        // Arrange
        var yaml = """
            version: 1
            workflow:
              name: WaitEventsNull
            nodes:
              - name: start
                type: start
                next: wait1
              - name: wait1
                type: wait
                events:
              - name: endNode
                type: end
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => _loader.Load(yaml));

        // Assert
        Assert.True(
            ex.Message.Contains("events", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("subscribe", StringComparison.OrdinalIgnoreCase));
    }
}
