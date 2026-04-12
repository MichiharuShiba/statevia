using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Join;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// IWorkflowEngine の実装。定義駆動型ワークフローエンジンの中核クラスです。
/// 事実駆動型 FSM、Fork/Join、Wait/Resume、協調的キャンセルをサポートします。
/// </summary>
public sealed partial class WorkflowEngine : IWorkflowEngine, IDisposable
{
    private readonly IScheduler _scheduler;
    private readonly IWorkflowInstanceIdGenerator _workflowInstanceIdGenerator;
    private readonly WorkflowExecutionLogger _workflowLog;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();

    public WorkflowEngine(WorkflowEngineOptions? options = null)
    {
        options ??= new WorkflowEngineOptions();
        _scheduler = new DefaultScheduler(options.MaxParallelism);
        _workflowInstanceIdGenerator = options.WorkflowInstanceIdGenerator ?? new UuidV7WorkflowInstanceIdGenerator();
        _workflowLog = new WorkflowExecutionLogger(ResolveLogger(options));
    }

    private static ILogger<WorkflowEngine> ResolveLogger(WorkflowEngineOptions options) =>
        options.Logger
        ?? options.LoggerFactory?.CreateLogger<WorkflowEngine>()
        ?? NullLogger<WorkflowEngine>.Instance;

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
        _workflowLog.LogWorkflowStarted(workflowId, definition.Name, definition.InitialState);
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
    public ApplyResult PublishEvent(string workflowId, string eventName, Guid clientEventId)
    {
        if (!_eventProviders.TryGetValue(workflowId, out var ep) || !_instances.TryGetValue(workflowId, out var instance))
        {
            return ApplyResult.Applied;
        }

        if (!instance.TryRegisterPublishClientEventId(clientEventId))
        {
            return ApplyResult.AlreadyApplied;
        }

        try
        {
            ep.Publish(eventName);
            return ApplyResult.Applied;
        }
        catch
        {
            instance.RemovePublishClientEventId(clientEventId);
            throw;
        }
    }

    /// <inheritdoc />
    public void PublishEvent(string eventName)
    {
        if (_eventProviders.IsEmpty)
        {
            return;
        }

        List<Exception>? publishFailures = null;
        foreach (var eventProvider in _eventProviders.Values.ToArray())
        {
            try
            {
                eventProvider.Publish(eventName);
            }
            catch (Exception exception)
            {
                publishFailures ??= new List<Exception>();
                publishFailures.Add(exception);
            }
        }

        if (publishFailures is { Count: > 0 })
        {
            throw publishFailures.Count == 1
                ? publishFailures[0]
                : new AggregateException(
                    "One or more workflows failed during PublishEvent broadcast.",
                    publishFailures);
        }
    }

    /// <inheritdoc />
    public ApplyResult PublishEvent(string eventName, Guid clientEventId)
    {
        if (_eventProviders.IsEmpty)
        {
            return ApplyResult.Applied;
        }

        var anyApplied = false;
        List<Exception>? publishFailures = null;
        foreach (var workflowId in _eventProviders.Keys.ToArray())
        {
            try
            {
                if (PublishEvent(workflowId, eventName, clientEventId).IsApplied)
                {
                    anyApplied = true;
                }
            }
            catch (Exception exception)
            {
                publishFailures ??= new List<Exception>();
                publishFailures.Add(exception);
            }
        }

        if (publishFailures is { Count: > 0 })
        {
            throw publishFailures.Count == 1
                ? publishFailures[0]
                : new AggregateException(
                    "One or more workflows failed during PublishEvent broadcast with clientEventId.",
                    publishFailures);
        }

        return anyApplied ? ApplyResult.Applied : ApplyResult.AlreadyApplied;
    }

    /// <inheritdoc />
    public async Task CancelAsync(string workflowId)
    {
        if (_instances.TryGetValue(workflowId, out var instance))
        {
            instance.MarkCancelled();
            _workflowLog.LogWorkflowCancelRequested(workflowId);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApplyResult> CancelAsync(string workflowId, Guid clientEventId)
    {
        if (!_instances.TryGetValue(workflowId, out var instance))
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return ApplyResult.Applied;
        }

        if (!instance.TryRegisterCancelClientEventId(clientEventId))
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return ApplyResult.AlreadyApplied;
        }

        instance.MarkCancelled();
        _workflowLog.LogWorkflowCancelRequested(workflowId);
        await Task.CompletedTask.ConfigureAwait(false);
        return ApplyResult.Applied;
    }

    public WorkflowSnapshot? GetSnapshot(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.Graph.ExportJson() : "{}";

    private async Task RunWorkflowAsync(WorkflowInstance instance, EventProvider eventProvider, object? workflowInput)
    {
        var initialInput = ApplyStateInput(instance, instance.Definition.InitialState, workflowInput);
        try
        {
            await ScheduleStateAsync(instance, eventProvider, instance.Definition.InitialState, null, null, initialInput).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types - workflow failure is observed via MarkFailed()
        catch (Exception ex)
        {
            instance.MarkFailed();
            _workflowLog.LogWorkflowRunFailed(ex, instance.WorkflowId, instance.Definition.Name);
        }
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

        _workflowLog.LogStateScheduled(instance.WorkflowId, stateName, nodeId);

        var ctx = new StateContext
        {
            Events = eventProvider,
            Store = new WorkflowStateStore(instance),
            WorkflowId = instance.WorkflowId,
            StateName = stateName,
            Logger = _workflowLog.CreateStateContextLogger(instance.WorkflowId, stateName)
        };

        var (fact, output) = await _scheduler.RunAsync(async ct =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var o = await executor.ExecuteAsync(ctx, input, ct).ConfigureAwait(false);
                instance.SetOutput(stateName, o);
                instance.Graph.CompleteNode(nodeId, Fact.Completed, o);
                sw.Stop();
                _workflowLog.LogStateCompleted(instance.WorkflowId, stateName, nodeId, Fact.Completed, sw.ElapsedMilliseconds);
                return (Fact.Completed, o);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                sw.Stop();
                _workflowLog.LogStateCompleted(instance.WorkflowId, stateName, nodeId, Fact.Cancelled, sw.ElapsedMilliseconds);
                return (Fact.Cancelled, (object?)null);
            }
#pragma warning disable CA1031 // Do not catch general exception types - state failure is recorded in graph
            catch (Exception ex)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                sw.Stop();
                _workflowLog.LogStateExecuteFailed(ex, instance.WorkflowId, stateName, nodeId, ex.GetType().Name);
                _workflowLog.LogStateCompleted(instance.WorkflowId, stateName, nodeId, Fact.Failed, sw.ElapsedMilliseconds);
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

        _workflowLog.LogJoinStateScheduled(instance.WorkflowId, joinStateName, nodeId);

        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);

        _workflowLog.LogJoinStateCompleted(instance.WorkflowId, joinStateName, nodeId, Fact.Joined);

        var transition = instance.Fsm.Evaluate(joinStateName, Fact.Joined);
        if (transition.HasTransition && transition.Next != null)
        {
            var mappedJoinInput = ApplyStateInput(instance, transition.Next, joinInputs);
            await ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, mappedJoinInput).ConfigureAwait(false);
        }
        else if (transition.End)
        {
            instance.MarkCompleted();
            _workflowLog.LogWorkflowCompleted(instance.WorkflowId, instance.Definition.Name);
        }
        else
        {
            _workflowLog.LogWarningNoTransition(instance.WorkflowId, joinStateName, Fact.Joined);
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
            _workflowLog.LogWorkflowTerminalFailure(instance.WorkflowId, instance.Definition.Name, stateName, fact);
            return;
        }

        var transition = instance.Fsm.Evaluate(stateName, fact);
        if (!transition.HasTransition)
        {
            if (!transition.End)
            {
                _workflowLog.LogWarningNoTransition(instance.WorkflowId, stateName, fact);
            }
            return;
        }
        if (transition.End)
        {
            instance.MarkCompleted();
            _workflowLog.LogWorkflowCompleted(instance.WorkflowId, instance.Definition.Name);
            return;
        }
        if (transition.Fork != null)
        {
            // Broadcast: 同一 output を各分岐の先頭状態へ渡す（workflow-input-output-spec §3.3）。
            foreach (var nextState in transition.Fork)
            {
                var mappedForkInput = ApplyStateInput(instance, nextState, output);
                _ = ScheduleStateAsync(instance, eventProvider, nextState, nodeId, EdgeType.Fork, mappedForkInput);
            }
        }
        else if (transition.Next != null)
        {
            var mappedInput = ApplyStateInput(instance, transition.Next, output);
            _ = ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, mappedInput);
        }
    }

    private object? ApplyStateInput(WorkflowInstance instance, string targetState, object? rawInput)
    {
        if (!instance.Definition.StateInputs.TryGetValue(targetState, out var spec))
        {
            return rawInput;
        }

        var evaluated = StateInputEvaluator.ApplyWithDiagnostics(spec, rawInput);
        foreach (var warning in evaluated.Warnings)
        {
            _workflowLog.LogWarningInputEvaluation(
                instance.WorkflowId,
                targetState,
                warning.InputKey,
                warning.Reason);
        }

        return evaluated.Value;
    }

    /// <inheritdoc />
    public void Dispose() => (_scheduler as IDisposable)?.Dispose();
}
