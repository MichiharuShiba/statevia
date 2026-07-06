using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Service.Api.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Service.Api.Application.Actions.Builtins;
using ActionExecutionTestSupport = Statevia.Service.Api.Tests.Application.Actions.Execution.ActionExecutionTestSupport;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Validation;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Application.Definition;
using Statevia.Service.Api.Hosting;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Service.Api.Tests.Hosting;

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

    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DefinitionCompilerService CreateSut(IActionCatalog? catalog = null)
    {
        catalog ??= ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        return new DefinitionCompilerService(
            catalog,
            provider.GetRequiredService<IActionVisibilityResolver>(),
            CreateDefaultStrategy(),
            provider,
            NullLogger<DefinitionCompilerService>.Instance);
    }

    private static void RegisterCustomInProcessAction(
        IActionCatalog catalog,
        string actionId,
        Func<StateContext, object?, CancellationToken, Task<object?>> execute)
    {
        var lastDot = actionId.LastIndexOf('.');
        var moduleId = lastDot > 0 ? actionId[..lastDot] : "test.module";
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = actionId,
                ModuleId = moduleId,
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Trusted,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Builtin,
            },
            new ActionCatalogEntry(InProcessFactory: _ => new DefaultStateExecutor(execute)));
    }

    private static DefinitionCompilerService CreateSutWithCatalog(out IActionCatalog catalog)
    {
        catalog = ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        return new DefinitionCompilerService(
            catalog,
            provider.GetRequiredService<IActionVisibilityResolver>(),
            CreateDefaultStrategy(),
            provider,
            NullLogger<DefinitionCompilerService>.Instance);
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
                action: my.vendor.missing
                on:
                  Completed:
                    end: true
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("Unknown action 'my.vendor.missing'", ex.Message, StringComparison.Ordinal);
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
    /// workflow.modules の alias 重複は Loader で失敗する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_DuplicateModuleAlias_ThrowsOnLoad()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
              modules:
                mail: com.company.mail
                Mail: com.company.other
            states:
              A:
                action: noop
                on:
                  Completed:
                    end: true
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("duplicate alias 'mail'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 同一状態にjoinとactionを併記した定義はレベル1検証で失敗する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_JoinAndActionInSameState_ThrowsLevel1ValidationFailed()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                join:
                  all: [B]
                on:
                  Joined:
                    end: true
              B:
                on:
                  Completed:
                    next: A
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
    /// action 省略状態は implicit noop（canonical FQCN）としてコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_ImplicitNoopAction_NormalizesToCanonical()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("A"));
    }

    /// <summary>
    /// builtin 短名 noop は canonical FQCN に正規化されてコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_BuiltinShortNameNoop_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("A"));
    }

    /// <summary>
    /// 廃止された delay5s を参照する定義はコンパイルできない。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_Delay5sBuiltin_ThrowsUnknownAction()
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

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));
        Assert.Contains("delay5s", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 組み込み sleep を参照する定義はコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_SleepBuiltin_Succeeds()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              Slow:
                action: sleep
                input:
                  duration: 5s
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

    /// <summary>他テナント所有 Action 参照はコンパイル 422 相当の例外になる。</summary>
    [Fact]
    public void ValidateAndCompile_OtherTenantAction_ThrowsNotVisible()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "tenant.only.action",
                ModuleId = "test.module",
                Version = "1.0.0",
                Visibility = ActionVisibility.Tenant,
                OwnerTenantId = "22222222-2222-2222-2222-222222222222",
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())));
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: tenant.only.action
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml, TestTenantId));

        // Assert
        Assert.Contains("not visible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// module alias と actionName は Compiler 経由で解決されコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_ModuleAliasResolvesActionName()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterCustomInProcessAction(
            catalog,
            "com.company.mail.send",
            (_, _, _) => Task.FromResult<object?>(null));
        var yaml = """
            workflow:
              name: W
              modules:
                mail: com.company.mail
            states:
              A:
                action: mail.send
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("A"));
    }

    /// <summary>
    /// 保存済み compiled JSON から定義を復元できる（action 解決込み）。
    /// </summary>
    [Fact]
    public void RestoreFromStoredVersion_RestoresCompiledDefinition()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: noop
                on:
                  Completed:
                    end: true
            """;
        var (_, compiledJson) = svc.ValidateAndCompile("W", yaml);

        // Act
        var restored = svc.RestoreFromStoredVersion(yaml, compiledJson);

        // Assert
        Assert.NotNull(restored.StateExecutorFactory.GetExecutor("A"));
    }

    /// <summary>保存済み bindings を再解決せず復元する（D1 決定論的実行）。</summary>
    [Fact]
    public void RestoreFromStoredVersion_UsesStoredBindingsWithoutReResolving()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterModuleVersion(catalog, "demo.module", "echo", "1.0.0");
        RegisterModuleVersion(catalog, "demo.module", "echo", "2.0.0");
        var yaml = """
            workflow:
              name: W
              modules:
                mail: demo.module@2.0.0
            states:
              A:
                action: mail.echo
                on:
                  Completed:
                    end: true
            """;
        var (_, compiledJson) = svc.ValidateAndCompile("W", yaml);
        RegisterModuleVersion(catalog, "demo.module", "echo", "3.0.0");

        // Act
        var restored = svc.RestoreFromStoredVersion(yaml, compiledJson);

        // Assert
        Assert.Equal("2.0.0", restored.StateActionBindings["A"].ResolvedModuleVersion);
        Assert.NotNull(restored.StateExecutorFactory.GetExecutor("A"));
    }

    /// <summary>ピン版が未ロードのときは MigrationRequired 相当エラーになる。</summary>
    [Fact]
    public void RestoreFromStoredVersion_WhenPinnedVersionNotLoaded_ThrowsMigrationRequired()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterModuleVersion(catalog, "demo.module", "echo", "1.0.0");
        var yaml = """
            workflow:
              name: W
              modules:
                mail: demo.module@1.0.0
            states:
              A:
                action: mail.echo
                on:
                  Completed:
                    end: true
            """;
        var (_, compiledJson) = svc.ValidateAndCompile("W", yaml);
        var catalogWithoutPinned = ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        RegisterModuleVersion(catalogWithoutPinned, "demo.module", "echo", "2.0.0");
        var provider = ActionExecutionTestSupport.CreateProvider(catalogWithoutPinned);
        var restoreSvc = new DefinitionCompilerService(
            catalogWithoutPinned,
            provider.GetRequiredService<IActionVisibilityResolver>(),
            CreateDefaultStrategy(),
            provider,
            NullLogger<DefinitionCompilerService>.Instance);

        // Act & Assert
        var ex = Assert.Throws<DefinitionMigrationRequiredException>(
            () => restoreSvc.RestoreFromStoredVersion(yaml, compiledJson));

        Assert.Contains("1.0.0", ex.Message, StringComparison.Ordinal);
    }

    private static void RegisterModuleVersion(
        IActionCatalog catalog,
        string moduleId,
        string actionName,
        string version)
    {
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = $"{moduleId}.{actionName}",
                ModuleId = moduleId,
                Version = version,
                TrustLevel = ActionTrustLevel.Trusted,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Builtin,
            },
            new ActionCatalogEntry(InProcessFactory: _ => new DefaultStateExecutor((_, _, _) => Task.FromResult<object?>(null))));
    }

    /// <summary>
    /// RegisterBuiltinActions に null registry を渡すと ArgumentNullException になる。
    /// </summary>
    [Fact]
    public void RegisterBuiltinActions_NullCatalog_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DefinitionCompilerService.RegisterBuiltinActions(null!));
    }

    /// <summary>
    /// 到達不能状態を含む定義は Level2 検証で失敗する。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_UnreachableState_ThrowsLevel2ValidationFailed()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              A:
                on:
                  Completed:
                    next: B
              B:
                on:
                  Completed:
                    end: true
              Orphan:
                on:
                  Completed:
                    end: true
            """;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("W", yaml));

        Assert.Contains("Level 2 validation failed", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 登録済みカスタムアクションを参照する定義はコンパイルできる。
    /// </summary>
    [Fact]
    public void ValidateAndCompile_RegisteredCustomAction_Succeeds()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterCustomInProcessAction(
            catalog,
            "my.custom.echo",
            (_, input, _) => Task.FromResult<object?>(input));
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: my.custom.echo
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
        var compiler = CreateSutWithCatalog(out var catalog);
        RegisterCustomInProcessAction(
            catalog,
            "my.custom.echo",
            (_, input, _) => Task.FromResult<object?>(input));
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: my.custom.echo
                on:
                  Completed:
                    end: true
            """;
        var (def, _) = compiler.ValidateAndCompile("W", yaml, TestTenantId);

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
    /// nodes の fork / join（all MVP）が Engine の join テーブルとして解決される。
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
        Assert.True(compiled.JoinTable.TryGetValue("join1", out var joinAll));
        Assert.Contains("b1", joinAll, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("b2", joinAll, StringComparer.OrdinalIgnoreCase);
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

    /// <summary>rest action の必須 input 欠落は schema 検証で失敗する。</summary>
    [Fact]
    public void ValidateAndCompile_RestMissingRequiredInput_ThrowsSchemaValidationException()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              Call:
                action: rest
                input:
                  method: GET
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

        // Assert
        Assert.Contains(ex.Errors, error => error.JsonPath == "$.input.url");
        Assert.Contains(ex.Errors, error => error.ActionId == WellKnownActionIds.Rest);
        Assert.Contains(ex.Errors, error => error.State == "Call");
    }

    /// <summary>rest action の未知 input プロパティは schema 検証で失敗する。</summary>
    [Fact]
    public void ValidateAndCompile_RestUnknownInputProperty_ThrowsSchemaValidationException()
    {
        // Arrange
        var svc = CreateSut();
        var yaml = """
            workflow:
              name: W
            states:
              Call:
                action: rest
                input:
                  url: https://example.test
                  method: GET
                  extra: true
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

        // Assert
        Assert.Contains(ex.Errors, error => error.JsonPath == "$.input.extra");
    }

    /// <summary>Publication 未登録 custom action は warning モードで compile 成功する。</summary>
    [Fact]
    public void ValidateAndCompile_CustomActionWithoutPublication_SucceedsWithWarningMode()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterCustomInProcessAction(
            catalog,
            "test.module.noschema",
            (_, _, _) => Task.FromResult<object?>(null));
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: test.module.noschema
                input:
                  anything: goes
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled);
    }

    /// <summary>strict モードでは Publication 未登録 action を拒否する。</summary>
    [Fact]
    public void ValidateAndCompile_CustomActionWithoutPublication_WhenStrict_ThrowsSchemaValidationException()
    {
        // Arrange
        var previous = ActionInputSchemaValidationOptions.RequireSchemaForUnpublishedActions;
        ActionInputSchemaValidationOptions.RequireSchemaForUnpublishedActions = true;
        try
        {
            var svc = CreateSutWithCatalog(out var catalog);
            RegisterCustomInProcessAction(
                catalog,
                "test.module.strict",
                (_, _, _) => Task.FromResult<object?>(null));
            var yaml = """
                workflow:
                  name: W
                states:
                  A:
                    action: test.module.strict
                    on:
                      Completed:
                        end: true
                """;

            // Act
            var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

            // Assert
            Assert.Contains(ex.Errors, error => error.JsonPath == "$.input");
            Assert.Equal("test.module.strict", ex.Errors[0].ActionId);
        }
        finally
        {
            ActionInputSchemaValidationOptions.RequireSchemaForUnpublishedActions = previous;
        }
    }

    /// <summary>Tenant Module action は publication 未登録で compile error になる。</summary>
    [Fact]
    public void ValidateAndCompile_TenantModuleActionWithoutPublication_ThrowsSchemaValidationException()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "test.module.nopub",
                ModuleId = "test.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Community,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Tenant,
                OwnerTenantId = TestTenantId.ToString("D"),
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())));
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: test.module.nopub
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

        // Assert
        Assert.Contains(ex.Errors, error => error.ActionId == "test.module.nopub");
    }

    /// <summary>Builtin noop は publication なしでも compile 成功する。</summary>
    [Fact]
    public void ValidateAndCompile_BuiltinNoop_StillSucceedsWithoutExplicitPublicationCheck()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out _);
        var yaml = """
            workflow:
              name: W
            states:
              A:
                action: statevia.action.builtin.noop
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled);
    }

    /// <summary>ネスト schema にドットキー input を指定すると compile 成功する（要件2 No.6）。</summary>
    [Fact]
    public void ValidateAndCompile_NestedInputWithDottedKeys_Succeeds()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterNestedShipTestAction(catalog);
        var yaml = """
            workflow:
              name: W
            states:
              Ship:
                action: test.module.nestedship
                input:
                  ship.address: 東京都
                  ship.contact.email: a@example.com
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("Ship"));
    }

    /// <summary>ネスト schema にネスト map input を指定すると compile 成功する（要件2 No.7）。</summary>
    [Fact]
    public void ValidateAndCompile_NestedInputWithNestedMap_Succeeds()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterNestedShipTestAction(catalog);
        var yaml = """
            workflow:
              name: W
            states:
              Ship:
                action: test.module.nestedship
                input:
                  ship:
                    address: 東京都
                    contact:
                      email: a@example.com
                on:
                  Completed:
                    end: true
            """;

        // Act
        var (compiled, _) = svc.ValidateAndCompile("W", yaml);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("Ship"));
    }

    /// <summary>ネスト必須欠落は階層 jsonPath で schema 検証失敗する（要件2 No.8）。</summary>
    [Fact]
    public void ValidateAndCompile_NestedRequiredMissing_ThrowsSchemaValidationException()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterNestedShipTestAction(catalog);
        var yaml = """
            workflow:
              name: W
            states:
              Ship:
                action: test.module.nestedship
                input:
                  ship.contact.email: a@example.com
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

        // Assert
        Assert.Contains(ex.Errors, error => error.JsonPath == "$.input.ship.address");
    }

    /// <summary>オブジェクトリテラルとドットキー子の競合は schema 検証で失敗する（要件2 No.9）。</summary>
    [Fact]
    public void ValidateAndCompile_NestedInputNormalizationConflict_ThrowsSchemaValidationException()
    {
        // Arrange
        var svc = CreateSutWithCatalog(out var catalog);
        RegisterNestedShipTestAction(catalog);
        var yaml = """
            workflow:
              name: W
            states:
              Ship:
                action: test.module.nestedship
                input:
                  ship: scalar-conflict
                  ship.address: 東京都
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ActionInputSchemaValidationException>(() => svc.ValidateAndCompile("W", yaml));

        // Assert
        Assert.Contains(
            ex.Errors,
            error => error.JsonPath == "$.input.ship"
                && error.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    private static void RegisterNestedShipTestAction(IActionCatalog catalog)
    {
        const string actionId = "test.module.nestedship";
        using var inputSchema = JsonDocument.Parse(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "ship": {
                  "type": "object",
                  "required": ["address"],
                  "additionalProperties": false,
                  "properties": {
                    "address": {
                      "type": "string",
                      "x-statevia-valueKind": "literalOrPath"
                    },
                    "contact": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "email": {
                          "type": "string",
                          "format": "email"
                        }
                      }
                    }
                  }
                }
              }
            }
            """);
        using var outputSchema = JsonDocument.Parse("""{"type":"object"}""");
        var publication = new Statevia.Core.Actions.Abstractions.Publication.ActionPublication(
            new Statevia.Core.Actions.Abstractions.Publication.ActionDescriptor(
                actionId,
                "1.0.0",
                "Nested Ship Test",
                Category: "Test"),
            new Statevia.Core.Actions.Abstractions.Publication.ActionSchemaBundle(
                JsonDocument.Parse(inputSchema.RootElement.GetRawText()),
                JsonDocument.Parse(outputSchema.RootElement.GetRawText())),
            new Statevia.Core.Actions.Abstractions.Publication.ActionUiMetadata(
                FieldOrder: ["ship"],
                Fields: new Dictionary<string, Statevia.Core.Actions.Abstractions.Publication.ActionFieldUiHints>
                {
                    ["ship"] = new Statevia.Core.Actions.Abstractions.Publication.ActionFieldUiHints(
                        LabelKey: $"{actionId}.ui.fields.ship.label"),
                    ["ship.address"] = new Statevia.Core.Actions.Abstractions.Publication.ActionFieldUiHints(
                        LabelKey: $"{actionId}.ui.fields.ship.address.label"),
                }));

        catalog.Register(
            new ActionDescriptor
            {
                ActionId = actionId,
                ModuleId = "test.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Trusted,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Builtin,
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())),
            publication);
    }

}


