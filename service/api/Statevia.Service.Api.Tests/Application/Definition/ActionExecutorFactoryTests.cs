using Statevia.Core.Actions.Abstractions.Catalog;
using ActionExecutionTestSupport = Statevia.Service.Api.Tests.Application.Actions.Execution.ActionExecutionTestSupport;
using Statevia.Service.Api.Application.Definition;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Tests.Application.Definition;

/// <summary><see cref="ActionExecutorFactory"/> の単体テスト。</summary>
public sealed class ActionExecutorFactoryTests
{
    private static (ActionExecutorFactory Factory, IActionCatalog Catalog) CreateSut(WorkflowDefinition definition)
    {
        var catalog = ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        return (new ActionExecutorFactory(definition, catalog, provider), catalog);
    }

    /// <summary>未定義の状態名を指定した場合は実行器を返さない。</summary>
    [Fact]
    public void GetExecutor_WhenStateNotFound_ReturnsNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(),
        };
        var (sut, _) = CreateSut(def);

        // Act / Assert
        Assert.Null(sut.GetExecutor("missing"));
    }

    /// <summary>wait 定義のみの状態に対して実行器を生成できる。</summary>
    [Fact]
    public void GetExecutor_WhenStateHasWait_ReturnsExecutor()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Wait = new WaitDefinition { Event = "E" },
                },
            },
        };
        var (sut, _) = CreateSut(def);

        // Act
        var executor = sut.GetExecutor("A");

        // Assert
        Assert.NotNull(executor);
    }

    /// <summary>空白アクション名の状態では NoOp を実行できる。</summary>
    [Fact]
    public async Task GetExecutor_WhenStateActionWhitespace_ExecutesNoOp()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Action = "  ",
                },
            },
        };
        var (sut, _) = CreateSut(def);
        var executor = sut.GetExecutor("A");
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

        // Act
        var output = await executor!.ExecuteAsync(ctx, null, CancellationToken.None);

        // Assert
        Assert.Null(output);
    }

    /// <summary>未登録アクションを指定した状態では実行器を返さない。</summary>
    [Fact]
    public void GetExecutor_WhenActionUnknown_ReturnsNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Action = "unknown.action",
                },
            },
        };
        var (sut, _) = CreateSut(def);

        // Act / Assert
        Assert.Null(sut.GetExecutor("A"));
    }
}
