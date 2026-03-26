using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Builtins;
using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Tests.Application.Definition;

public sealed class ActionExecutorFactoryTests
{
    /// <summary>
    /// 未定義の状態名を指定した場合は実行器を返さない。
    /// </summary>
    [Fact]
    public void GetExecutor_WhenStateNotFound_ReturnsNull()
    {
        // Arrange
        var registry = new InMemoryActionRegistry();

        // Act
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "W" },
            States = new Dictionary<string, StateDefinition>()
        };

        var sut = new ActionExecutorFactory(def, registry);

        // Assert
        Assert.Null(sut.GetExecutor("missing"));
    }

    /// <summary>
    /// wait定義のみの状態に対して実行器を生成できる。
    /// </summary>
    [Fact]
    public void GetExecutor_WhenStateHasWait_ReturnsExecutor()
    {
        // Arrange
        var registry = new InMemoryActionRegistry();

        // Act
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "W" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Wait = new WaitDefinition { Event = "E" }
                }
            }
        };

        var sut = new ActionExecutorFactory(def, registry);

        var executor = sut.GetExecutor("A");
        // Assert
        Assert.NotNull(executor);
    }

    /// <summary>
    /// 空白アクション名の状態ではNoOp実行器を選択する。
    /// </summary>
    [Fact]
    public void GetExecutor_WhenStateActionWhitespace_UsesNoOpExecutor()
    {
        // Arrange
        var noopExecutor = DefaultStateExecutor.Create(new NoOpState());
        var registry = new InMemoryActionRegistry();
        registry.Register(WellKnownActionIds.NoOp, noopExecutor);

        // Act
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "W" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Action = "  "
                }
            }
        };

        var sut = new ActionExecutorFactory(def, registry);

        var executor = sut.GetExecutor("A");
        // Assert
        Assert.Same(noopExecutor, executor);
    }

    /// <summary>
    /// 未登録アクションを指定した状態では実行器を返さない。
    /// </summary>
    [Fact]
    public void GetExecutor_WhenActionUnknown_ReturnsNull()
    {
        // Arrange
        var registry = new InMemoryActionRegistry();

        // Act
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "W" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Action = "unknown.action"
                }
            }
        };

        var sut = new ActionExecutorFactory(def, registry);

        // Assert
        Assert.Null(sut.GetExecutor("A"));
    }
}

