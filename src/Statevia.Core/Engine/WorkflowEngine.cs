using System.Collections.Concurrent;
using Statevia.Core.Abstractions;
using Statevia.Core.Execution;
using Statevia.Core.ExecutionGraphs;
using Statevia.Core.FSM;
using Statevia.Core.Join;
using Statevia.Core.Scheduler;

namespace Statevia.Core.Engine;

/// <summary>
/// IWorkflowEngine の実装。定義駆動型ワークフローエンジンの中核クラスです。
/// 事実駆動型 FSM、Fork/Join、Wait/Resume、協調的キャンセルをサポートします。
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IScheduler _scheduler;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();

    public WorkflowEngine(WorkflowEngineOptions? options = null)
    {
        options ??= new WorkflowEngineOptions();
        _scheduler = new DefaultScheduler(options.MaxParallelism);
    }

    /// <inheritdoc />
    public string Start(CompiledWorkflowDefinition definition)
    {
        var workflowId = Guid.NewGuid().ToString("N")[..12];
        var eventProvider = new EventProvider(workflowId);
        var instance = new WorkflowInstance
        {
            WorkflowId = workflowId,
            Definition = definition,
            Fsm = new TransitionTable(definition.Transitions),
            JoinTracker = new JoinTracker(definition),
            Graph = new ExecutionGraph()
        };
        _instances[workflowId] = instance;
        _eventProviders[workflowId] = eventProvider;
        _ = RunWorkflowAsync(instance, eventProvider);
        return workflowId;
    }

    /// <inheritdoc />
    public void PublishEvent(string eventName)
    {
        foreach (var ep in _eventProviders.Values) ep.Publish(eventName);
    }

    /// <inheritdoc />
    public async Task CancelAsync(string workflowId)
    {
        if (_instances.TryGetValue(workflowId, out var instance)) instance.MarkCancelled();
        await Task.CompletedTask;
    }

    public WorkflowSnapshot? GetSnapshot(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.Graph.ExportJson() : "{}";

    private async Task RunWorkflowAsync(WorkflowInstance instance, EventProvider eventProvider)
    {
        try { await ScheduleStateAsync(instance, eventProvider, instance.Definition.InitialState, null, null, null); }
        catch { instance.MarkFailed(); }
    }

    private async Task ScheduleStateAsync(WorkflowInstance instance, EventProvider eventProvider, string stateName, string? fromNodeId, EdgeType? edgeType, object? input)
    {
        var def = instance.Definition;
        if (def.JoinTable.ContainsKey(stateName))
        {
            await RunJoinStateAsync(instance, eventProvider, stateName, fromNodeId, edgeType);
            return;
        }

        var executor = def.StateExecutorFactory.GetExecutor(stateName);
        if (executor == null) return;

        var nodeId = instance.Graph.AddNode(stateName);
        if (fromNodeId != null && edgeType != null) instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        instance.AddActiveState(stateName);

        var ctx = new StateContext
        {
            Events = eventProvider,
            Store = new WorkflowStateStore(instance),
            WorkflowId = instance.WorkflowId,
            StateName = stateName
        };

        var (fact, output) = await _scheduler.RunAsync(async ct =>
        {
            try
            {
                var o = await executor.ExecuteAsync(ctx, input, ct);
                instance.SetOutput(stateName, o);
                instance.Graph.CompleteNode(nodeId, Fact.Completed, o);
                return (Fact.Completed, o);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                return (Fact.Cancelled, (object?)null);
            }
            catch
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                return (Fact.Failed, (object?)null);
            }
            finally { instance.RemoveActiveState(stateName); }
        });

        ProcessFact(instance, eventProvider, stateName, fact, output, nodeId);
    }

    private async Task RunJoinStateAsync(WorkflowInstance instance, EventProvider eventProvider, string joinStateName, string? fromNodeId, EdgeType? edgeType)
    {
        var def = instance.Definition;
        var joinInputs = instance.JoinTracker.GetJoinInputs(joinStateName);
        var nodeId = instance.Graph.AddNode(joinStateName);
        if (fromNodeId != null && edgeType != null) instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);

        var transition = instance.Fsm.Evaluate(joinStateName, Fact.Joined);
        if (transition.HasTransition && transition.Next != null)
            await ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, joinInputs);
        else if (transition.End) instance.MarkCompleted();
    }

    private void ProcessFact(WorkflowInstance instance, EventProvider eventProvider, string stateName, string fact, object? output, string nodeId)
    {
        var def = instance.Definition;
        var readyJoin = instance.JoinTracker.RecordFact(stateName, fact, output);
        if (readyJoin != null) { _ = RunJoinStateAsync(instance, eventProvider, readyJoin, nodeId, EdgeType.Join); return; }
        if (fact == Fact.Failed || fact == Fact.Cancelled) { instance.MarkFailed(); instance.MarkCancelled(); return; }

        var transition = instance.Fsm.Evaluate(stateName, fact);
        if (!transition.HasTransition) return;
        if (transition.End) { instance.MarkCompleted(); return; }
        if (transition.Fork != null)
            foreach (var nextState in transition.Fork)
                _ = ScheduleStateAsync(instance, eventProvider, nextState, nodeId, EdgeType.Fork, null);
        else if (transition.Next != null)
            _ = ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, null);
    }
}
