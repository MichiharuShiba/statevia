using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
/// <remarks>
/// <para>
/// ホストが <see cref="IWorkflowEngine"/> を Singleton として登録する場合、コンストラクタに渡した
/// <see cref="IScheduler"/> はプロセス内のワークフロー実行で共有される（グローバル並列制御の単一窓口）。
/// </para>
/// <para>
/// 例外を広く捕捉する <c>#pragma warning disable CA1031</c> 付きの try/catch は、複数ワークフローへのイベントブロードキャストで
/// 失敗を集約するため、状態実行失敗をグラフへ記録するため、およびログ実装の失敗を遷移へ伝播させない（STV-404）ためである。
/// </para>
/// </remarks>
public sealed partial class WorkflowEngine : IWorkflowEngine, IDisposable
{
    private readonly IScheduler _scheduler;
    private readonly IWorkflowInstanceFactory _instanceFactory;
    private readonly IWorkflowInstanceIdGenerator _workflowInstanceIdGenerator;
    private readonly WorkflowExecutionLogger _workflowLog;
    private readonly string _workerId;
    private readonly ConcurrentDictionary<string, WorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();
    private Func<string, Task>? _nodeCompletedHandler;

    /// <summary>
    /// 依存を注入してエンジンを構築する。
    /// </summary>
    /// <param name="scheduler">状態実行のスケジューリング（並列度は実装側で解釈）。</param>
    /// <param name="instanceFactory">ワークフローインスタンスの組み立て。</param>
    /// <param name="workflowInstanceIdGenerator"><see cref="IWorkflowEngine.Start"/> で ID 未指定のときに使う生成器。</param>
    /// <param name="logger">エンジン本体の構造化ログ。</param>
    public WorkflowEngine(
        IScheduler scheduler,
        IWorkflowInstanceFactory instanceFactory,
        IWorkflowInstanceIdGenerator workflowInstanceIdGenerator,
        ILogger<WorkflowEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(instanceFactory);
        ArgumentNullException.ThrowIfNull(workflowInstanceIdGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _scheduler = scheduler;
        _instanceFactory = instanceFactory;
        _workflowInstanceIdGenerator = workflowInstanceIdGenerator;
        _workflowLog = new WorkflowExecutionLogger(logger);
        _workerId = Guid.NewGuid().ToString("D");
    }

    /// <inheritdoc />
    public string Start(CompiledWorkflowDefinition definition, string? workflowId = null, object? workflowInput = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        workflowId ??= _workflowInstanceIdGenerator.NewWorkflowInstanceId();
        var eventProvider = new EventProvider(workflowId);
        var instance = _instanceFactory.Create(definition, workflowId);
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
#pragma warning disable CA1031 // 複数ワークフローへブロードキャストするため、例外種別に依らず収集して AggregateException にまとめる
            catch (Exception exception)
            {
                publishFailures ??= new List<Exception>();
                publishFailures.Add(exception);
            }
#pragma warning restore CA1031
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
#pragma warning disable CA1031 // 複数ワークフローへブロードキャストするため、例外種別に依らず収集して AggregateException にまとめる
            catch (Exception exception)
            {
                publishFailures ??= new List<Exception>();
                publishFailures.Add(exception);
            }
#pragma warning restore CA1031
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

    /// <inheritdoc />
    public WorkflowSnapshot? GetSnapshot(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string workflowId) =>
        _instances.TryGetValue(workflowId, out var instance) ? instance.Graph.ExportJson() : "{}";

    /// <inheritdoc />
    public void SetNodeCompletedHandler(Func<string, Task>? handler)
    {
        _nodeCompletedHandler = handler;
    }

    private async Task RunWorkflowAsync(WorkflowInstance instance, EventProvider eventProvider, object? workflowInput)
    {
        var initialInput = ApplyStateInput(instance, instance.Definition.InitialState, workflowInput);
        try
        {
            await ScheduleStateAsync(instance, eventProvider, instance.Definition.InitialState, null, null, initialInput).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、ワークフロー失敗は MarkFailed により観測する
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

        var attempt = instance.NextAttempt(stateName);
        var nodeType = ResolveNodeType(def, stateName);
        var waitKey = def.WaitTable.TryGetValue(stateName, out var configuredWaitKey)
            ? configuredWaitKey
            : null;
        var nodeId = instance.Graph.AddNode(
            stateName,
            nodeType: nodeType,
            input: input,
            attempt: attempt,
            workerId: _workerId,
            waitKey: waitKey);
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
                await NotifyNodeCompletedAsync(instance.WorkflowId).ConfigureAwait(false);
                sw.Stop();
                _workflowLog.LogStateCompleted(instance.WorkflowId, stateName, nodeId, Fact.Completed, sw.ElapsedMilliseconds);
                return (Fact.Completed, o);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                await NotifyNodeCompletedAsync(instance.WorkflowId).ConfigureAwait(false);
                sw.Stop();
                _workflowLog.LogStateCompleted(instance.WorkflowId, stateName, nodeId, Fact.Cancelled, sw.ElapsedMilliseconds);
                return (Fact.Cancelled, (object?)null);
            }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、状態失敗は実行グラフへ記録する
            catch (Exception ex)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                await NotifyNodeCompletedAsync(instance.WorkflowId).ConfigureAwait(false);
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
        if (!instance.JoinTracker.TryBeginJoinExecution(joinStateName))
        {
            return;
        }

        var joinInputs = instance.JoinTracker.GetJoinInputs(joinStateName);
        var joinSourceNodeIds = instance.JoinTracker.GetJoinSourceNodeIds(joinStateName);
        var attempt = instance.NextAttempt(joinStateName);
        var nodeId = instance.Graph.AddNode(joinStateName, nodeType: "Join", input: joinInputs, attempt: attempt, workerId: _workerId);
        if (edgeType == EdgeType.Join)
        {
            foreach (var sourceNodeId in joinSourceNodeIds)
            {
                instance.Graph.AddEdge(sourceNodeId, nodeId, EdgeType.Join);
            }
        }
        else if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }

        _workflowLog.LogJoinStateScheduled(instance.WorkflowId, joinStateName, nodeId);

        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);
        await NotifyNodeCompletedAsync(instance.WorkflowId).ConfigureAwait(false);

        _workflowLog.LogJoinStateCompleted(instance.WorkflowId, joinStateName, nodeId, Fact.Joined);

        var (transition, routingDiag) = EvaluateTransition(instance, joinStateName, Fact.Joined, joinInputs);
        if (routingDiag is not null)
        {
            instance.Graph.SetNodeConditionRouting(nodeId, routingDiag);
        }

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

    /// <summary>
    /// ノード完了通知ハンドラへベストエフォートで通知する。
    /// </summary>
    private async Task NotifyNodeCompletedAsync(string workflowId)
    {
        var handler = _nodeCompletedHandler;
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(workflowId).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 外部ハンドラの失敗は遷移へ伝播させずログのみ（STV-404 のベストエフォート通知）
        catch (Exception exception)
        {
            _workflowLog.LogWarningNodeCompletedHandlerFailed(exception, workflowId);
        }
#pragma warning restore CA1031
    }

    private void ProcessFact(WorkflowInstance instance, EventProvider eventProvider, string stateName, string fact, object? output, string nodeId)
    {
        var readyJoin = instance.JoinTracker.RecordFact(stateName, fact, output, nodeId);
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

        var (transition, routingDiag) = EvaluateTransition(instance, stateName, fact, output);
        if (routingDiag is not null)
        {
            instance.Graph.SetNodeConditionRouting(nodeId, routingDiag);
        }

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

    private (TransitionResult Transition, ConditionRoutingDiagnostics? RoutingDiagnostics) EvaluateTransition(
        WorkflowInstance instance,
        string stateName,
        string fact,
        object? output)
    {
        if (instance.Definition.ConditionalTransitions.TryGetValue(stateName, out var stateTransitions)
            && stateTransitions.TryGetValue(fact, out var compiledTransition))
        {
            var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
                compiledTransition,
                fact,
                output,
                onPathWarning: (path, reason) =>
                    _workflowLog.LogWarningConditionPathResolution(instance.WorkflowId, stateName, fact, path, reason));
            return (transition, diagnostics);
        }

        return (instance.Fsm.Evaluate(stateName, fact), null);
    }

    /// <inheritdoc />
    public void Dispose() => (_scheduler as IDisposable)?.Dispose();

    private static string ResolveNodeType(CompiledWorkflowDefinition def, string stateName)
    {
        if (string.Equals(def.InitialState, stateName, StringComparison.OrdinalIgnoreCase))
            return "Start";
        if (def.JoinTable.ContainsKey(stateName))
            return "Join";
        if (def.WaitTable.ContainsKey(stateName))
            return "Wait";
        if (def.ForkTable.ContainsKey(stateName))
            return "Fork";
        if (def.Transitions.TryGetValue(stateName, out var byFact)
            && byFact.Values.Any(target => target.End))
            return "End";
        return "Task";
    }
}
