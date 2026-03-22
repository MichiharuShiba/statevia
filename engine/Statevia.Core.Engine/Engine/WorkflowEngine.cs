using System.Collections.Concurrent;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Join;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// IWorkflowEngine の実装。定義駆動型ワークフローエンジンの中核クラスです。
/// 事実駆動型 FSM、Fork/Join、Wait/Resume、協調的キャンセルをサポートします。
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine, IDisposable
{
    private readonly IScheduler _scheduler;
    private readonly IWorkflowInstanceIdGenerator _workflowInstanceIdGenerator;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();

    public WorkflowEngine(WorkflowEngineOptions? options = null)
    {
        options ??= new WorkflowEngineOptions();
        _scheduler = new DefaultScheduler(options.MaxParallelism);
        _workflowInstanceIdGenerator = options.WorkflowInstanceIdGenerator ?? new UuidV7WorkflowInstanceIdGenerator();
    }

    /// <inheritdoc />
    public string Start(CompiledWorkflowDefinition definition, string? workflowId = null, object? workflowInput = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        workflowId ??= _workflowInstanceIdGenerator.NewWorkflowInstanceId();
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
        _ = RunWorkflowAsync(instance, eventProvider, workflowInput);
        return workflowId;
    }

    /// <inheritdoc />
    public void PublishEvent(string workflowId, string eventName)
    {
        if (_eventProviders.TryGetValue(workflowId, out var ep))
        {
            ep.Publish(eventName);
        }
    }

    /// <inheritdoc />
    public void PublishEvent(string eventName)
    {
        foreach (var ep in _eventProviders.Values)
        {
            ep.Publish(eventName);
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(string workflowId)
    {
        if (_instances.TryGetValue(workflowId, out var instance))
        {
            instance.MarkCancelled();
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public WorkflowSnapshot? GetSnapshot(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.Graph.ExportJson() : "{}";

    private async Task RunWorkflowAsync(WorkflowInstance instance, EventProvider eventProvider, object? workflowInput)
    {
        try { await ScheduleStateAsync(instance, eventProvider, instance.Definition.InitialState, null, null, workflowInput).ConfigureAwait(false); }
#pragma warning disable CA1031 // Do not catch general exception types - workflow failure is observed via MarkFailed()
        catch (Exception) { instance.MarkFailed(); }
#pragma warning restore CA1031
    }

    private async Task ScheduleStateAsync(WorkflowInstance instance, EventProvider eventProvider, string stateName, string? fromNodeId, EdgeType? edgeType, object? input)
    {
        var def = instance.Definition;
        if (def.JoinTable.ContainsKey(stateName))
        {
            await RunJoinStateAsync(instance, eventProvider, stateName, fromNodeId, edgeType).ConfigureAwait(false);
            return;
        }

        var executor = def.StateExecutorFactory.GetExecutor(stateName);
        if (executor == null)
        {
            return;
        }

        var nodeId = instance.Graph.AddNode(stateName);
        if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }
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
                var o = await executor.ExecuteAsync(ctx, input, ct).ConfigureAwait(false);
                instance.SetOutput(stateName, o);
                instance.Graph.CompleteNode(nodeId, Fact.Completed, o);
                return (Fact.Completed, o);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                return (Fact.Cancelled, (object?)null);
            }
#pragma warning disable CA1031 // Do not catch general exception types - state failure is recorded in graph
            catch (Exception)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                return (Fact.Failed, (object?)null);
            }
#pragma warning restore CA1031
            finally { instance.RemoveActiveState(stateName); }
        }).ConfigureAwait(false);

        ProcessFact(instance, eventProvider, stateName, fact, output, nodeId);
    }

    private async Task RunJoinStateAsync(WorkflowInstance instance, EventProvider eventProvider, string joinStateName, string? fromNodeId, EdgeType? edgeType)
    {
        var joinInputs = instance.JoinTracker.GetJoinInputs(joinStateName);
        var nodeId = instance.Graph.AddNode(joinStateName);
        if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }
        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);

        var transition = instance.Fsm.Evaluate(joinStateName, Fact.Joined);
        if (transition.HasTransition && transition.Next != null)
        {
            await ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, joinInputs).ConfigureAwait(false);
        }
        else if (transition.End)
        {
            instance.MarkCompleted();
        }
    }

    private void ProcessFact(WorkflowInstance instance, EventProvider eventProvider, string stateName, string fact, object? output, string nodeId)
    {
        var readyJoin = instance.JoinTracker.RecordFact(stateName, fact, output);
        if (readyJoin != null)
        {
            _ = RunJoinStateAsync(instance, eventProvider, readyJoin, nodeId, EdgeType.Join);
            return;
        }
        if (fact is Fact.Failed or Fact.Cancelled)
        {
            instance.MarkFailed();
            instance.MarkCancelled();
            return;
        }

        var transition = instance.Fsm.Evaluate(stateName, fact);
        if (!transition.HasTransition)
        {
            return;
        }
        if (transition.End)
        {
            instance.MarkCompleted();
            return;
        }
        if (transition.Fork != null)
        {
            // Broadcast: 同一 output を各分岐の先頭状態へ渡す（workflow-input-output-spec §3.3）。
            foreach (var nextState in transition.Fork)
            {
                _ = ScheduleStateAsync(instance, eventProvider, nextState, nodeId, EdgeType.Fork, output);
            }
        }
        else if (transition.Next != null)
        {
            _ = ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, output);
        }
    }

    /// <inheritdoc />
    public void Dispose() => (_scheduler as IDisposable)?.Dispose();
}
