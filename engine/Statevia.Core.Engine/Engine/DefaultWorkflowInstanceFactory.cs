using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="IWorkflowInstanceFactory"/> の既定実装。FSM・Join・実行グラフを従来と同順で組み立てる。
/// </summary>
public sealed class DefaultWorkflowInstanceFactory : IWorkflowInstanceFactory
{
    /// <inheritdoc />
    public WorkflowInstance Create(CompiledWorkflowDefinition definition, string workflowId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        return new WorkflowInstance
        {
            WorkflowId = workflowId,
            Definition = definition,
            Fsm = new TransitionTable(definition.Transitions),
            JoinTracker = new JoinTracker(definition),
            Graph = new ExecutionGraph()
        };
    }
}
