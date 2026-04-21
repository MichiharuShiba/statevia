using System.Text.Json;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Actions.Abstractions;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Tests.Hosting;

public sealed class DefinitionCompilerServiceTests
{
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
    /// custom.echo実行時の出力グラフに入力値が反映される。
    /// </summary>
    [Fact]
    public async Task Start_CustomEchoAction_OutputReflectsWorkflowInput()
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
        var engine = new WorkflowEngine();
        var wfId = engine.Start(def, workflowInput: new Dictionary<string, int> { ["x"] = 42 });

        await Task.Delay(200);

        var json = engine.ExportExecutionGraph(wfId);
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
        Assert.True(root.TryGetProperty("conditionalTransitions", out var ct) || root.TryGetProperty("ConditionalTransitions", out ct));
        Assert.Equal(JsonValueKind.Object, ct.ValueKind);
        Assert.True(ct.EnumerateObject().Any());
        Assert.True(root.TryGetProperty("stateInputs", out var si) || root.TryGetProperty("StateInputs", out si));
        Assert.Equal(JsonValueKind.Object, si.ValueKind);
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


