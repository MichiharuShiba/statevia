using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class WorkflowSnapshotExtensionsTests
{
    /// <summary><see cref="WorkflowSnapshotExtensions.ToSnapshot"/> がインスタンスの観測可能な状態を写し取ることを検証する。</summary>
    [Fact]
    public void ToSnapshot_MapsWorkflowFieldsAndFlags()
    {
        // Arrange
        var execFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var definition = new CompiledWorkflowDefinition
        {
            Name = "UnitFlow",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(
                StringComparer.OrdinalIgnoreCase),
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            WaitTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            InitialState = "S0",
            StateExecutorFactory = execFactory
        };
        var factory = new DefaultWorkflowInstanceFactory();
        var instance = factory.Create(definition, "wf-abc");
        instance.AddActiveState("S1");
        instance.MarkCompleted();

        // Act
        var snapshot = instance.ToSnapshot();

        // Assert
        Assert.Equal("wf-abc", snapshot.WorkflowId);
        Assert.Equal("UnitFlow", snapshot.WorkflowName);
        Assert.Single(snapshot.ActiveStates);
        Assert.Equal("S1", snapshot.ActiveStates[0]);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsCancelled);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>インスタンスが null のとき <see cref="ArgumentNullException"/> をスローすることを検証する。</summary>
    [Fact]
    public void ToSnapshot_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        WorkflowInstance? instance = null;

        // Act / Assert
        var ex = Assert.Throws<ArgumentNullException>(() => WorkflowSnapshotExtensions.ToSnapshot(instance!));
        Assert.Equal("instance", ex.ParamName);
    }
}
