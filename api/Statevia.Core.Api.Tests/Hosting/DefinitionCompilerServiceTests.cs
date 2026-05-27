using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Api.Tests.Hosting;

public sealed class DefinitionCompilerServiceTests
{
    private static ExecutionEngine CreateTestEngine(int maxParallelism = 4) =>
        new(
            new DefaultScheduler(maxParallelism),
            new DefaultExecutionInstanceFactory(),
            new UuidV7ExecutionIdGenerator(),
            NullLoggerFactory.Instance);

    private static IDefinitionLoadStrategy CreateDefaultStrategy() =>
        new DefinitionLoadStrategy(new StateWorkflowDefinitionLoader(), new NodesWorkflowDefinitionLoader());

    private static DefinitionCompilerService CreateSut(IActionRegistry? registry = null)
    {
        registry ??= new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        return new DefinitionCompilerService(registry, CreateDefaultStrategy());
    }

    /// <summary>
    /// 未登録アクションを含む定義は対象状態名付きで例外になる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_UnknownAction_ThrowsWithMessage()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: missing.action
                on:
                  Completed:
                    end: true
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("Unknown action 'missing.action'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("state 'A'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同一状態にwaitとactionを併記した定義はレベル1検証で失敗する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_WaitAndActionInSameState_ThrowsLevel1ValidationFailed()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                wait:
                  event: E
                on:
                  Completed:
                    end: true
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("Level 1 validation failed", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// waitのみの状態を含む定義は正常にコンパイルされる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_WaitWithoutAction_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                wait:
                  event: E
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled);
        var exec = compiled.StateExecutorFactory.GetExecutor("A");
        Assert.NotNull(exec);
    }

    /// <summary>
    /// 組み込み delay5s を参照する定義はコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_Delay5sBuiltin_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              Slow:
                action: delay5s
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled);
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("Slow"));
    }

    /// <summary>
    /// 登録済みカスタムアクションを参照する定義はコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_RegisteredCustomAction_Succeeds()
    {
        // Arrange
        var registry = new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        registry.Register(
            "custom.echo",
            new DefaultStateExecutor((_, input, _) => Task.FromResult<object?>(input)));
        var svc = new DefinitionCompilerService(registry, CreateDefaultStrategy());
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: custom.echo
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        var exec = compiled.StateExecutorFactory.GetExecutor("A");
        // Assert
        Assert.NotNull(exec);
    }

    /// <summary>
    /// custom.echo 実行時の出力グラフに Start 時の <c>input</c> が反映される。
    /// </summary>
    [Fact]
    public async Task Start_CustomEchoAction_OutputReflectsInput()
    {
        // Arrange
        var registry = new InMemoryActionRegistry();
        DefinitionCompilerService.RegisterBuiltinActions(registry);
        registry.Register(
            "custom.echo",
            new DefaultStateExecutor((_, input, _) => Task.FromResult<object?>(input)));
        var compiler = new DefinitionCompilerService(registry, CreateDefaultStrategy());
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: custom.echo
                on:
                  Completed:
                    end: true
            """;
        var (def, _) = compiler.ValidateAndCompile("W", yaml);

        // Act
        var engine = CreateTestEngine();
        var executionId = engine.Start(def, input: new Dictionary<string, int> { ["x"] = 42 });

        await Task.Delay(200);

        var json = engine.ExportExecutionGraph(executionId);
        // Assert
        Assert.Contains("42", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// ルートに nodes 配列がある場合は NodesWorkflowDefinitionLoader 経由で states に変換されコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesRoot_Succeeds()
    {
        var svc = CreateSut();
        var yaml = """
            version: 1
            workflow:
              name: N
            nodes:
              - id: start
                type: start
                next: endNode
              - id: endNode
                type: end
            """;

        var (compiled, _) = svc.ValidateAndCompile("N", yaml);

        Assert.NotNull(compiled);
        Assert.Equal("start", compiled.InitialState, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// nodes の fork / join（allOf MVP）が Engine の join テーブルとして解決される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesForkJoin_Succeeds()
    {
        var svc = CreateSut();
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

        var (compiled, _) = svc.ValidateAndCompile("ForkJoin", yaml);

        Assert.NotNull(compiled.JoinTable);
        Assert.True(compiled.JoinTable.TryGetValue("join1", out var allOf));
        Assert.Contains("b1", allOf, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("b2", allOf, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// nodes の単一無条件 edges は next と等価に扱われ、next と併記時は同一先のみ受理する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesSingleUnconditionalEdge_EquivalentToNext_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            version: 1
            workflow:
              name: EdgeNextEquiv
            nodes:
              - id: start
                type: start
                next: a
              - id: a
                type: action
                action: noop
                next: endNode
                edges:
                  - to: endNode
              - id: endNode
                type: end
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("EdgeNextEquiv", yaml);

        // Assert
        Assert.NotNull(compiled);
    }

    /// <summary>
    /// start ノードが next ではなく単一無条件 edges のみを持つ場合でも受理される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesStartWithEdgesOnly_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
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
        var (compiled, _) = svc.ValidateAndCompile("StartEdgesOnly", yaml);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("start", compiled.InitialState, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// nodes の next と単一無条件 edges の遷移先が不一致のときは ArgumentException。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesNextAndUnconditionalEdgeMismatch_ThrowsArgumentException()
    {
        // Arrange
        var svc = CreateSut();
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

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("EdgeNextMismatch", yaml));

        Assert.Contains("must match", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// nodes の条件付き edges は on.Completed の cases/default に正規化され Level1 を通過する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesConditionalEdges_NormalizesToCasesDefault_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
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
        var (compiled, _) = svc.ValidateAndCompile("ConditionalEdges", yaml);

        // Assert
        Assert.NotNull(compiled);
    }

    /// <summary>
    /// nodes の action.error は on.Failed.next へ変換される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesActionError_AddsOnFailedTransition_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
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
        var (compiled, _) = svc.ValidateAndCompile("FailedPath", yaml);

        // Assert
        Assert.NotNull(compiled);
        Assert.True(compiled.Transitions.TryGetValue("a", out var byFact));
        Assert.Equal("failedHandler", byFact["Failed"].Next);
    }

    /// <summary>
    /// nodes の action.error が { id } 形式でも正規化され on.Failed.next に変換される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesActionErrorObject_NormalizesAndCompiles()
    {
        // Arrange
        var svc = CreateSut();
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
        var (compiled, _) = svc.ValidateAndCompile("FailedPathObj", yaml);

        // Assert
        Assert.NotNull(compiled);
        Assert.Equal("failedHandler", compiled.Transitions["a"]["Failed"].Next);
    }

    /// <summary>
    /// wait ノードで error を指定した定義は拒否される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesErrorOnWait_ThrowsArgumentException()
    {
        // Arrange
        var svc = CreateSut();
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

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("ErrorOnWait", yaml));
        Assert.Contains("'error' is not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// action.error が自己参照を指す定義は拒否される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesActionErrorSelfReference_ThrowsArgumentException()
    {
        // Arrange
        var svc = CreateSut();
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

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("ErrorSelf", yaml));
        Assert.Contains("self-reference", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// action.error が未定義ノードを指す定義は拒否される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesActionErrorUnknownTarget_ThrowsArgumentException()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            version: 1
            workflow:
              name: ErrorUnknown
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

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("ErrorUnknown", yaml));
        Assert.Contains("references unknown id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// コンパイル結果 JSON に conditionalTransitions と stateInputs が含まれる（T6 デバッグ返却）。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_CompiledJson_IncludesConditionalTransitionsAndStateInputs()
    {
        // Arrange
        var svc = CreateSut();
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
        var (_, json) = svc.ValidateAndCompile("ConditionalEdges", yaml);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("name", out var name));
        Assert.False(root.TryGetProperty("Name", out _));
        Assert.Equal("ConditionalEdges", name.GetString());
        Assert.True(root.TryGetProperty("conditionalTransitions", out var ct));
        Assert.False(root.TryGetProperty("ConditionalTransitions", out _));
        Assert.Equal(JsonValueKind.Object, ct.ValueKind);
        Assert.True(ct.EnumerateObject().Any());
        Assert.True(root.TryGetProperty("stateInputs", out var si));
        Assert.False(root.TryGetProperty("StateInputs", out _));
        Assert.Equal(JsonValueKind.Object, si.ValueKind);
    }

    /// <summary>
    /// nodes の noop で Start 時の <c>input</c> が伝播するとき、<c>$.eligible eq true</c> がマッチすること（公式サンプル相当）。
    /// </summary>
    [Fact]
    public async Task ValidateAndCompile_NodesEligibleEqTrueWithJsonInput_ResolvesMatchedCase()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            version: 1
            workflow:
              name: CustomerOrderParallel
              id: sample.customer.order.parallel
            nodes:
              - id: order.start
                type: start
                next: order.preflight
              - id: order.preflight
                type: action
                action: noop
                edges:
                  - to: order.validate
                    when:
                      path: $.eligible
                      op: eq
                      value: true
                    order: 10
                  - to: order.reject.notify
              - id: order.validate
                type: action
                action: noop
                next: order.end
              - id: order.reject.notify
                type: action
                action: noop
                next: order.end
              - id: order.end
                type: end
            """;

        var (compiled, _) = svc.ValidateAndCompile("CustomerOrderParallel", yaml);
        using var engine = CreateTestEngine(maxParallelism: 1);
        using var inputDoc = JsonDocument.Parse("""{"eligible":true,"shared":{"orderId":"ORD-1001"}}""");

        // Act
        var executionId = engine.Start(compiled, null, inputDoc.RootElement);
        await Task.Delay(400);
        var graphJson = engine.ExportExecutionGraph(executionId);

        // Assert
        using var graphDoc = JsonDocument.Parse(graphJson);
        var preflightNode = graphDoc.RootElement.GetProperty("nodes").EnumerateArray()
            .First(n =>
                string.Equals(n.GetProperty("stateName").GetString(), "order.preflight", StringComparison.Ordinal));
        Assert.True(preflightNode.TryGetProperty("conditionRouting", out var routing));
        Assert.Equal(ConditionRoutingResolutions.MatchedCase, routing.GetProperty("resolution").GetString());
        Assert.Equal(0, routing.GetProperty("matchedCaseIndex").GetInt32());
    }

    /// <summary>
    /// nodes 配列と states オブジェクトの併存は U10 に従い ArgumentException。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesAndStatesBoth_ThrowsArgumentException()
    {
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: X
            nodes:
              - id: a
                type: start
                next: b
            states:
              A:
                on:
                  Completed:
                    end: true
            """;

        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("X", yaml));

        Assert.Contains("both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// states 形式の input で ${...} テンプレートは拒否される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_StatesInputTemplate_ThrowsArgumentException()
    {
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: S
            states:
              A:
                action: noop
                input: ${input.orderId}
                on:
                  Completed:
                    end: true
            """;

        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("S", yaml));
        Assert.Contains("${...}", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// nodes 形式の action.input で不正な $. パスは拒否される。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_NodesInputInvalidPath_ThrowsArgumentException()
    {
        var svc = CreateSut();
        var yaml = """
            version: 1
            workflow:
              name: N
            nodes:
              - id: start
                type: start
                next: act
              - id: act
                type: action
                action: noop
                input:
                  orderId: $.input.-orderId
                next: endNode
              - id: endNode
                type: end
            """;

        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("N", yaml));
        Assert.Contains("invalid input path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}


