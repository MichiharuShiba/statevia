using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="IExecutionInstanceFactory"/> の既定実装。FSM・Join・実行グラフを従来と同順で組み立てる。
/// </summary>
public sealed class DefaultExecutionInstanceFactory : IExecutionInstanceFactory
{
    /// <inheritdoc />
    public ExecutionInstance Create(CompiledWorkflowDefinition definition, string executionId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

        return new ExecutionInstance
        {
            ExecutionId = executionId,
            Definition = definition,
            Fsm = new TransitionTable(definition.Transitions),
            JoinTracker = new JoinTracker(definition),
            Graph = new ExecutionGraph()
        };
    }
}
