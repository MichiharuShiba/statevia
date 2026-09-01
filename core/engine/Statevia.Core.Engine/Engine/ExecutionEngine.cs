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
/// <see cref="IExecutionEngine"/> の実装。定義駆動型実行エンジンの中核クラスです。
/// コンパイル済みワークフロー定義に基づき、事実駆動型 FSM、Fork/Join、Wait/Resume、協調的キャンセルをサポートします。
/// </summary>
/// <remarks>
/// <para>
/// ホストが <see cref="IExecutionEngine"/> を Singleton として登録する場合、コンストラクタに渡した
/// <see cref="IScheduler"/> はプロセス内の実行インスタンスで共有される（グローバル並列制御の単一窓口）。
/// </para>
/// <para>
/// 例外を広く捕捉する <c>#pragma warning disable CA1031</c> 付きの try/catch は、複数実行インスタンスへのイベントブロードキャストで
/// 失敗を集約するため、状態実行失敗をグラフへ記録するため、およびログ実装の失敗を遷移へ伝播させないためである。
/// </para>
/// </remarks>
public sealed partial class ExecutionEngine : IExecutionEngine, IDisposable
{
    private readonly IScheduler _scheduler;
    private readonly IExecutionInstanceFactory _instanceFactory;
    private readonly IExecutionIdGenerator _executionIdGenerator;
    private readonly ExecutionEngineLogger _executionLog;
    private readonly string _workerId;
    private readonly ConcurrentDictionary<string, ExecutionInstance> _instances = new();
    private readonly ConcurrentDictionary<string, EventProvider> _eventProviders = new();
    private Func<string, Task>? _nodeCompletedHandler;
    private Func<string, string, Task>? _suspendHandler;
    private Func<ForkExpansionEvent, Task>? _forkExpansionHandler;

    /// <summary>
    /// 依存を注入してエンジンを構築する。
    /// </summary>
    /// <param name="scheduler">状態実行のスケジューリング（並列度は実装側で解釈）。</param>
    /// <param name="instanceFactory">実行インスタンスの組み立て。</param>
    /// <param name="executionIdGenerator"><see cref="IExecutionEngine.Start"/> で ID 未指定のときに使う生成器。</param>
    /// <param name="loggerFactory">実行ログ用 <see cref="ExecutionEngineLogger"/> の生成に使うファクトリ。</param>
    public ExecutionEngine(
        IScheduler scheduler,
        IExecutionInstanceFactory instanceFactory,
        IExecutionIdGenerator executionIdGenerator,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(instanceFactory);
        ArgumentNullException.ThrowIfNull(executionIdGenerator);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _scheduler = scheduler;
        _instanceFactory = instanceFactory;
        _executionIdGenerator = executionIdGenerator;
        _executionLog = new ExecutionEngineLogger(loggerFactory.CreateLogger<ExecutionEngineLogger>());
        _workerId = Guid.NewGuid().ToString("D");
    }

    /// <inheritdoc />
    public string Start(
        CompiledWorkflowDefinition definition,
        string? executionId = null,
        object? input = null,
        string? initialState = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        executionId ??= _executionIdGenerator.NewExecutionId();
        var startState = string.IsNullOrWhiteSpace(initialState)
            ? definition.InitialState
            : initialState.Trim();
        var eventProvider = CreateEventProvider(executionId);
        var instance = _instanceFactory.Create(definition, executionId);
        _instances[executionId] = instance;
        _eventProviders[executionId] = eventProvider;
        _executionLog.LogExecutionStarted(executionId, definition.Name, startState);
        _ = RunExecutionAsync(instance, eventProvider, input, startState);
        return executionId;
    }

    private EventProvider CreateEventProvider(string executionId)
    {
        var eventProvider = new EventProvider(executionId);
        eventProvider.OnNodeWaitRegistered = nodeId => NotifySuspend(executionId, nodeId);
        return eventProvider;
    }

    private void NotifySuspend(string executionId, string nodeId)
    {
        var handler = _suspendHandler;
        if (handler is null)
        {
            return;
        }

        _ = NotifySuspendAsync(handler, executionId, nodeId);
    }

    private async Task NotifySuspendAsync(
        Func<string, string, Task> handler,
        string executionId,
        string nodeId)
    {
        try
        {
            await handler(executionId, nodeId).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // suspend 通知失敗は実行継続を妨げない
        catch (Exception ex)
        {
            _executionLog.LogExecutionRunFailed(ex, executionId, "suspend-handler");
        }
#pragma warning restore CA1031
    }

    private void NotifyForkExpansion(
        string executionId,
        string forkState,
        string sourceNodeId,
        IReadOnlyList<ForkBranchExpansionPlan> branches)
    {
        var handler = _forkExpansionHandler;
        if (handler is null)
            return;

        var evt = new ForkExpansionEvent(executionId, forkState, sourceNodeId, branches);
        _ = NotifyForkExpansionAsync(handler, evt);
    }

    private async Task NotifyForkExpansionAsync(
        Func<ForkExpansionEvent, Task> handler,
        ForkExpansionEvent evt)
    {
        try
        {
            await handler(evt).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Fork 展開通知失敗は呼び出し元の再試行／親 Failed に委ね、Engine ループは止めない
        catch (Exception ex)
        {
            _executionLog.LogExecutionRunFailed(ex, evt.ExecutionId, "fork-expansion-handler");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public void ResumeWaitNode(string executionId, string nodeId, string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        if (!_eventProviders.TryGetValue(executionId, out var eventProvider)
            || !_instances.TryGetValue(executionId, out var instance))
        {
            throw new InvalidOperationException($"Execution '{executionId}' was not found.");
        }

        var node = instance.Graph.GetNodesSnapshot()
            .FirstOrDefault(n => string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            throw new InvalidOperationException($"Node '{nodeId}' was not found in execution '{executionId}'.");
        }

        if (!string.Equals(node.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
            || node.CompletedAt is not null)
        {
            throw new InvalidOperationException(
                $"Node '{nodeId}' is not an active Wait node in execution '{executionId}'.");
        }

        // EventProvider.Resume と route table キー（Load 時 Trim）に合わせて正規化する。
        var trimmedEventName = eventName.Trim();
        if (!instance.Definition.WaitEventRouteTable.TryGetValue(node.NodeName, out var routes)
            || !routes.ContainsKey(trimmedEventName))
        {
            throw new InvalidOperationException(
                $"Event '{trimmedEventName}' is not allowed for Wait state '{node.NodeName}'.");
        }

        eventProvider.Resume(nodeId, trimmedEventName);
    }

    /// <inheritdoc />
    public void PublishEvent(string executionId, string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        if (!_instances.TryGetValue(executionId, out var instance))
        {
            return;
        }

        var activeWaits = instance.Graph.GetNodesSnapshot()
            .Where(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && n.CompletedAt is null)
            .ToList();
        if (activeWaits.Count != 1)
        {
            throw new InvalidOperationException(
                $"PublishEvent requires exactly one active Wait node (found {activeWaits.Count}).");
        }

        var waitNode = activeWaits[0];
        if (!instance.Definition.WaitEventRouteTable.TryGetValue(waitNode.NodeName, out var routes)
            || routes.Count != 1)
        {
            throw new InvalidOperationException(
                $"PublishEvent requires the active Wait state '{waitNode.NodeName}' to have exactly one event.");
        }

        // シムは「唯一の許可イベント」と呼び出し eventName が一致するときだけ委譲する。
        var soleAllowedEvent = routes.Keys.First();
        var trimmedEventName = eventName.Trim();
        if (!string.Equals(soleAllowedEvent, trimmedEventName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PublishEvent event '{trimmedEventName}' does not match the sole allowed event '{soleAllowedEvent}' for Wait state '{waitNode.NodeName}'.");
        }

        ResumeWaitNode(executionId, waitNode.NodeId, soleAllowedEvent);
    }

    /// <inheritdoc />
    public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId)
    {
        if (!_instances.TryGetValue(executionId, out var instance))
        {
            return ApplyResult.Applied;
        }

        if (!instance.TryRegisterPublishClientEventId(clientEventId))
        {
            return ApplyResult.AlreadyApplied;
        }

        try
        {
            PublishEvent(executionId, eventName);
            return ApplyResult.Applied;
        }
        catch
        {
            instance.RemovePublishClientEventId(clientEventId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(string executionId)
    {
        if (_instances.TryGetValue(executionId, out var instance))
        {
            instance.MarkCancelled();
            _executionLog.LogExecutionCancelRequested(executionId);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId)
    {
        if (!_instances.TryGetValue(executionId, out var instance))
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
        _executionLog.LogExecutionCancelRequested(executionId);
        await Task.CompletedTask.ConfigureAwait(false);
        return ApplyResult.Applied;
    }

    /// <inheritdoc />
    public ExecutionSnapshot? GetSnapshot(string executionId) =>
        _instances.TryGetValue(executionId, out var instance) ? instance.ToSnapshot() : null;

    /// <inheritdoc />
    public string ExportExecutionGraph(string executionId) =>
        _instances.TryGetValue(executionId, out var instance) ? instance.Graph.ExportJson() : "{}";

    /// <inheritdoc />
    public void SetNodeCompletedHandler(Func<string, Task>? handler)
    {
        _nodeCompletedHandler = handler;
    }

    /// <inheritdoc />
    public void SetSuspendHandler(Func<string, string, Task>? handler)
    {
        _suspendHandler = handler;
    }

    /// <inheritdoc />
    public void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler)
    {
        _forkExpansionHandler = handler;
    }

    /// <inheritdoc />
    public ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        return _instances.TryGetValue(executionId, out var instance)
            ? instance.ExportRuntimeCheckpoint()
            : null;
    }

    /// <inheritdoc />
    public void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.SchemaVersion != ExecutionRuntimeCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported checkpoint schema version {checkpoint.SchemaVersion}.");
        }

        if (!string.Equals(definition.Name, checkpoint.DefinitionName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Definition name mismatch: checkpoint='{checkpoint.DefinitionName}', provided='{definition.Name}'.");
        }

        var executionId = checkpoint.ExecutionId;
        if (_instances.ContainsKey(executionId))
        {
            throw new InvalidOperationException($"Execution '{executionId}' is already loaded.");
        }

        var eventProvider = CreateEventProvider(executionId);
        var instance = _instanceFactory.Create(definition, executionId);
        instance.ApplyRuntimeCheckpoint(checkpoint);
        _instances[executionId] = instance;
        _eventProviders[executionId] = eventProvider;

        if (checkpoint.IsCompleted || checkpoint.IsCancelled || checkpoint.IsFailed)
        {
            return;
        }

        foreach (var wait in checkpoint.PendingWaits)
        {
            _ = ContinueRestoredWaitAsync(instance, eventProvider, wait);
        }

        // NodeCompleted 時点の checkpoint は次遷移前のため、完了済みで出辺のないノードから継続する。
        ContinueCompletedFrontierAfterImport(instance, eventProvider);
    }

    /// <inheritdoc />
    public bool Unload(string executionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        if (!_instances.TryRemove(executionId, out var instance))
        {
            _eventProviders.TryRemove(executionId, out _);
            return false;
        }

        instance.Dispose();

        if (_eventProviders.TryRemove(executionId, out var eventProvider))
        {
            eventProvider.OnNodeWaitRegistered = null;
            eventProvider.AbortForUnload();
        }

        return true;
    }

    private async Task ContinueRestoredWaitAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        CheckpointPendingWait wait)
    {
        instance.AddActiveState(wait.NodeName);
        try
        {
            var (fact, output) = await _scheduler.RunAsync(async ct =>
            {
                try
                {
                    var eventName = await eventProvider
                        .WaitForEventAsync(wait.NodeId, wait.AllowedEvents, notifySuspend: false, ct)
                        .ConfigureAwait(false);
                    object result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["event"] = eventName
                    };
                    instance.SetOutput(wait.NodeName, result);
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Completed, result);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    return (Fact.Completed, result);
                }
                catch (ExecutionUnloadException)
                {
                    return ((string?)null, (object?)null);
                }
                catch (OperationCanceledException)
                {
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Cancelled, null);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    return (Fact.Cancelled, (object?)null);
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    instance.Graph.CompleteNode(wait.NodeId, Fact.Failed, null);
                    await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                    _executionLog.LogStateExecuteFailed(
                        ex,
                        instance.ExecutionId,
                        wait.NodeName,
                        wait.NodeId,
                        ex.GetType().Name);
                    return (Fact.Failed, (object?)null);
                }
#pragma warning restore CA1031
            }, instance.ActionCancellationToken).ConfigureAwait(false);

            if (fact is null)
            {
                return;
            }

            ProcessFact(instance, eventProvider, wait.NodeName, fact, output, wait.NodeId);
        }
        finally
        {
            instance.RemoveActiveState(wait.NodeName);
        }
    }

    /// <summary>
    /// Import 後、完了済みで出辺のないノードから遷移を再開する（recovery / ステップ checkpoint 用）。
    /// </summary>
    /// <remarks>
    /// <see cref="NotifyNodeCompletedAsync"/> 時点の checkpoint は <see cref="ProcessFact"/> 前のため
    /// 出辺が無い。Wait 未完了は <see cref="ContinueRestoredWaitAsync"/> が担当する。
    /// </remarks>
    private void ContinueCompletedFrontierAfterImport(ExecutionInstance instance, EventProvider eventProvider)
    {
        var nodes = instance.Graph.GetNodesSnapshot();
        var nodesWithOutgoing = instance.Graph.GetEdgesSnapshot()
            .Select(edge => edge.From)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node.CompletedAt is null || string.IsNullOrWhiteSpace(node.Fact))
            {
                continue;
            }

            if (nodesWithOutgoing.Contains(node.NodeId))
            {
                continue;
            }

            // 未完了 Wait は PendingWaits 経路。完了済み Wait で出辺なしなら ProcessFact が必要。
            instance.RemoveActiveState(node.NodeName);
            instance.TryGetOutput(node.NodeName, out var output);
            ProcessFact(instance, eventProvider, node.NodeName, node.Fact, output, node.NodeId);
        }
    }

    private async Task RunExecutionAsync(
        ExecutionInstance instance,
        EventProvider eventProvider,
        object? input,
        string startState)
    {
        instance.InitializeContext(input);
        var initialInput = ApplyStateInput(instance, startState, input);
        try
        {
            await ScheduleStateAsync(instance, eventProvider, startState, null, null, initialInput).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、実行失敗は MarkFailed により観測する
        catch (Exception ex)
        {
            instance.MarkFailed();
            _executionLog.LogExecutionRunFailed(ex, instance.ExecutionId, instance.Definition.Name);
        }
#pragma warning restore CA1031
    }

    private async Task ScheduleStateAsync(ExecutionInstance instance, EventProvider eventProvider, string stateName, string? fromNodeId, EdgeType? edgeType, object? input)
    {
        var def = instance.Definition;
        if (def.JoinTable.ContainsKey(stateName))
        {
            // Hosted 物理子: 親側 Join に到達してもローカル事実が揃わないときは
            // Join を実行せず子 execution として終端する（ネスト Fork の内側→外側 Join）。
            // 通常のアクション完了時の「next が Join なら MarkCompleted」と同契約。
            if (_forkExpansionHandler is not null
                && !instance.JoinTracker.IsJoinReadyToExecute(stateName))
            {
                instance.MarkCompleted(input);
                _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                return;
            }

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
        var waitMetadata = BuildWaitNodeGraphMetadata(def, stateName);

        var nodeId = instance.Graph.AddNode(
            stateName,
            nodeType: nodeType,
            input: input,
            attempt: attempt,
            workerId: _workerId,
            wait: waitMetadata);
        if (fromNodeId != null && edgeType != null)
        {
            instance.Graph.AddEdge(fromNodeId, nodeId, edgeType.Value);
        }
        instance.AddActiveState(stateName);

        _executionLog.LogStateScheduled(instance.ExecutionId, stateName, nodeId);

        var ctx = new StateContext
        {
            Events = eventProvider,
            Store = new ExecutionStateStore(instance),
            ExecutionId = instance.ExecutionId,
            StateName = stateName,
            NodeId = nodeId,
            Logger = _executionLog.CreateStateContextLogger(instance.ExecutionId, stateName)
        };

        var (fact, output) = await _scheduler.RunAsync(async ct =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var o = await executor.ExecuteAsync(ctx, input, ct).ConfigureAwait(false);
                instance.SetOutput(stateName, o);
                instance.Graph.CompleteNode(nodeId, Fact.Completed, o);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Completed, sw.ElapsedMilliseconds);
                return (Fact.Completed, o);
            }
            catch (ExecutionUnloadException)
            {
                sw.Stop();
                return ((string?)null, (object?)null);
            }
            catch (OperationCanceledException)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Cancelled, null);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Cancelled, sw.ElapsedMilliseconds);
                return (Fact.Cancelled, (object?)null);
            }
#pragma warning disable CA1031 // 例外種別に依らず捕捉し、状態失敗は実行グラフへ記録する
            catch (Exception ex)
            {
                instance.Graph.CompleteNode(nodeId, Fact.Failed, null);
                await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);
                sw.Stop();
                _executionLog.LogStateExecuteFailed(ex, instance.ExecutionId, stateName, nodeId, ex.GetType().Name);
                _executionLog.LogStateCompleted(instance.ExecutionId, stateName, nodeId, Fact.Failed, sw.ElapsedMilliseconds);
                return (Fact.Failed, (object?)null);
            }
#pragma warning restore CA1031
            finally { instance.RemoveActiveState(stateName); }
        }, instance.ActionCancellationToken).ConfigureAwait(false);

        if (fact is null)
        {
            return;
        }

        ProcessFact(instance, eventProvider, stateName, fact, output, nodeId);
    }

    private async Task RunJoinStateAsync(ExecutionInstance instance, EventProvider eventProvider, string joinStateName, string? fromNodeId, EdgeType? edgeType)
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

        _executionLog.LogJoinStateScheduled(instance.ExecutionId, joinStateName, nodeId);

        instance.Graph.CompleteNode(nodeId, Fact.Joined, null);
        await NotifyNodeCompletedAsync(instance.ExecutionId).ConfigureAwait(false);

        _executionLog.LogJoinStateCompleted(instance.ExecutionId, joinStateName, nodeId, Fact.Joined);

        // Join の集約 input を当該 State の output として Context へ記録する。
        // これにより when.path の $.states.<Join>.output… 参照と output: $.vars… 代入が
        // 通常 State と同じ規則で機能する（未記録だと条件が常に path_not_found になる）。
        instance.SetOutput(joinStateName, joinInputs);

        var (transition, routingDiag) = EvaluateTransition(instance, joinStateName, Fact.Joined);
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
            instance.MarkCompleted(joinInputs);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
        }
        else
        {
            _executionLog.LogWarningNoTransition(instance.ExecutionId, joinStateName, Fact.Joined);
        }
    }

    /// <summary>
    /// ノード完了通知ハンドラへベストエフォートで通知する。
    /// </summary>
    private async Task NotifyNodeCompletedAsync(string executionId)
    {
        var handler = _nodeCompletedHandler;
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(executionId).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 外部ハンドラの失敗は遷移へ伝播させずログのみ（ログ失敗を遷移に伝播させないベストエフォート通知）
        catch (Exception exception)
        {
            _executionLog.LogWarningNodeCompletedHandlerFailed(exception, executionId);
        }
#pragma warning restore CA1031
    }

    private void ProcessFact(ExecutionInstance instance, EventProvider eventProvider, string stateName, string fact, object? output, string nodeId)
    {
        var readyJoin = instance.JoinTracker.RecordFact(stateName, fact, output, nodeId);
        if (readyJoin != null)
        {
            if (instance.IsCancelled)
            {
                _executionLog.LogExecutionCancelSuppressedTransition(
                    instance.ExecutionId,
                    instance.Definition.Name,
                    stateName,
                    fact);
                return;
            }

            _ = RunJoinStateAsync(instance, eventProvider, readyJoin, nodeId, EdgeType.Join);
            return;
        }
        if (fact is Fact.Failed or Fact.Cancelled)
        {
            instance.MarkFailed();
            instance.MarkCancelled();
            _executionLog.LogExecutionTerminalFailure(instance.ExecutionId, instance.Definition.Name, stateName, fact);
            return;
        }

        if (instance.IsCancelled)
        {
            _executionLog.LogExecutionCancelSuppressedTransition(
                instance.ExecutionId,
                instance.Definition.Name,
                stateName,
                fact);
            return;
        }

        ConditionRoutingDiagnostics? routingDiag = null;
        TransitionResult transition;
        if (TryResolveWaitEventRoute(instance.Definition, stateName, fact, output, out var waitTransition))
        {
            transition = waitTransition;
        }
        else
        {
            (transition, routingDiag) = EvaluateTransition(instance, stateName, fact);
        }

        if (routingDiag is not null)
        {
            instance.Graph.SetNodeConditionRouting(nodeId, routingDiag);
        }

        if (!transition.HasTransition)
        {
            if (!transition.End)
            {
                _executionLog.LogWarningNoTransition(instance.ExecutionId, stateName, fact);
            }
            return;
        }
        if (transition.End)
        {
            instance.MarkCompleted(output);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
            return;
        }

        ScheduleTransitionContinuation(instance, eventProvider, stateName, transition, output, nodeId);
    }

    /// <summary>
    /// Fork / Next 遷移を Hosted 展開または同一インスタンス Schedule に振り分ける。
    /// </summary>
    private void ScheduleTransitionContinuation(
        ExecutionInstance instance,
        EventProvider eventProvider,
        string stateName,
        TransitionResult transition,
        object? output,
        string nodeId)
    {
        if (transition.Fork != null)
        {
            // Broadcast: 同一 output を各分岐の先頭状態へ写像（workflow-input-output-spec §3.3）。
            var plans = transition.Fork
                .Select(nextState => new ForkBranchExpansionPlan(
                    nextState,
                    ApplyStateInput(instance, nextState, output)))
                .ToList();

            if (_forkExpansionHandler is not null)
            {
                // Hosted: 物理子展開。同一インスタンス上では分岐を走らせない。
                NotifyForkExpansion(instance.ExecutionId, stateName, nodeId, plans);
                return;
            }

            foreach (var plan in plans)
            {
                _ = ScheduleStateAsync(
                    instance,
                    eventProvider,
                    plan.BranchState,
                    nodeId,
                    EdgeType.Fork,
                    plan.MappedInput);
            }

            return;
        }

        if (transition.Next is null)
        {
            return;
        }

        // Hosted 物理子: Join は親が集約する。子は分岐終端として完了する。
        if (_forkExpansionHandler is not null
            && instance.Definition.JoinTable.ContainsKey(transition.Next))
        {
            instance.MarkCompleted(output);
            _executionLog.LogExecutionCompleted(instance.ExecutionId, instance.Definition.Name);
            return;
        }

        var mappedInput = ApplyStateInput(instance, transition.Next, output);
        _ = ScheduleStateAsync(instance, eventProvider, transition.Next, nodeId, EdgeType.Next, mappedInput);
    }

    /// <inheritdoc />
    public void CompletePhysicalJoin(
        string executionId,
        string joinStateName,
        IReadOnlyDictionary<string, object?> branchOutputs,
        IReadOnlyList<PhysicalJoinContextFragment>? contextMerges = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinStateName);
        ArgumentNullException.ThrowIfNull(branchOutputs);

        if (!_instances.TryGetValue(executionId, out var instance)
            || !_eventProviders.TryGetValue(executionId, out var eventProvider))
        {
            throw new InvalidOperationException($"Execution '{executionId}' is not loaded.");
        }

        if (!instance.Definition.JoinTable.ContainsKey(joinStateName))
        {
            throw new InvalidOperationException(
                $"Join state '{joinStateName}' is not defined for execution '{executionId}'.");
        }

        if (contextMerges is { Count: > 0 })
        {
            foreach (var fragment in contextMerges)
            {
                if (fragment.States is { Count: > 0 })
                    instance.Context.MergeStateEntries(fragment.States);
                if (fragment.Vars is not null)
                    instance.Context.MergeVars(fragment.Vars);
            }
        }

        // 循環再入場: 前回訪問の startedJoins / 観測事実を捨ててから物理 Join を再実行する。
        instance.JoinTracker.ResetForPhysicalJoinReentry(joinStateName);

        foreach (var (branchState, branchOutput) in branchOutputs)
        {
            instance.JoinTracker.RecordFact(branchState, Fact.Completed, branchOutput);
        }

        _ = RunJoinStateAsync(instance, eventProvider, joinStateName, fromNodeId: null, edgeType: null);
    }

    private object? ApplyStateInput(ExecutionInstance instance, string targetState, object? rawInput)
    {
        if (!instance.Definition.StateInputs.TryGetValue(targetState, out var spec))
        {
            return rawInput;
        }

        var evaluated = StateInputEvaluator.ApplyWithDiagnostics(spec, instance.Context, rawInput);
        foreach (var warning in evaluated.Warnings)
        {
            _executionLog.LogWarningInputEvaluation(
                instance.ExecutionId,
                targetState,
                warning.InputKey,
                warning.Reason);
        }

        return evaluated.Value;
    }

    private (TransitionResult Transition, ConditionRoutingDiagnostics? RoutingDiagnostics) EvaluateTransition(
        ExecutionInstance instance,
        string stateName,
        string fact)
    {
        if (instance.Definition.ConditionalTransitions.TryGetValue(stateName, out var stateTransitions)
            && stateTransitions.TryGetValue(fact, out var compiledTransition))
        {
            var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
                compiledTransition,
                fact,
                instance.Context,
                onPathWarning: (path, reason) =>
                    _executionLog.LogWarningConditionPathResolution(instance.ExecutionId, stateName, fact, path, reason));
            return (transition, diagnostics);
        }

        return (instance.Fsm.Evaluate(stateName, fact), null);
    }

    /// <summary>
    /// Wait 完了時に <see cref="CompiledWorkflowDefinition.WaitEventRouteTable"/> から次状態を解決する。
    /// </summary>
    /// <remarks>
    /// <para>次状態は route table が正本。監査用 output の event 名はルックアップキーとしてのみ使う。</para>
    /// <para>ルートにイベントはあるが <c>Next</c> が空のときは false を返し、通常の FSM（on.Completed）へフォールバックする。</para>
    /// </remarks>
    private static bool TryResolveWaitEventRoute(
        CompiledWorkflowDefinition definition,
        string stateName,
        string fact,
        object? output,
        out TransitionResult transition)
    {
        transition = TransitionResult.None;
        if (!string.Equals(fact, Fact.Completed, StringComparison.Ordinal))
        {
            return false;
        }

        if (!definition.WaitEventRouteTable.TryGetValue(stateName, out var routes))
        {
            return false;
        }

        if (!TryExtractWaitEventName(output, out var eventName))
        {
            return false;
        }

        if (!routes.TryGetValue(eventName, out var route))
        {
            // ルートに無いイベントは Wait ルート解決済みとして FSM へ落とさない。
            return true;
        }

        // Next 未設定（旧 wait.event + on.Completed など）は通常の FSM 評価に委ねる。
        if (string.IsNullOrWhiteSpace(route.Next))
        {
            return false;
        }

        transition = TransitionResult.ToNext(route.Next);
        return true;
    }

    /// <summary>Wait 監査 output からイベント名を取り出す。</summary>
    private static bool TryExtractWaitEventName(object? output, out string eventName)
    {
        eventName = string.Empty;
        switch (output)
        {
            case IReadOnlyDictionary<string, string> stringMap
                when stringMap.TryGetValue("event", out var fromString)
                     && !string.IsNullOrWhiteSpace(fromString):
                eventName = fromString.Trim();
                return true;
            case IReadOnlyDictionary<string, object?> objectMap
                when objectMap.TryGetValue("event", out var raw)
                     && raw is string fromObject
                     && !string.IsNullOrWhiteSpace(fromObject):
                eventName = fromObject.Trim();
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var instance in _instances.Values)
        {
            instance.Dispose();
        }

        _instances.Clear();
        (_scheduler as IDisposable)?.Dispose();
    }

    /// <summary>コンパイル済み Wait 情報からグラフ用メタデータを構築する。</summary>
    private static WaitNodeGraphMetadata? BuildWaitNodeGraphMetadata(
        CompiledWorkflowDefinition def,
        string stateName)
    {
        List<string>? allowedEvents = null;
        string? waitKey = null;
        if (def.WaitEventRouteTable.TryGetValue(stateName, out var routes) && routes.Count > 0)
        {
            allowedEvents = routes.Keys.ToList();
            if (allowedEvents.Count == 1)
                waitKey = allowedEvents[0];
        }

        IReadOnlyList<WaitSubscriptionSnapshot>? subscriptions = null;
        if (def.WaitSubscriptions.TryGetValue(stateName, out var waitSubscriptions)
            && waitSubscriptions.Count > 0)
        {
            subscriptions = waitSubscriptions
                .Select(s => new WaitSubscriptionSnapshot
                {
                    Topic = s.Topic,
                    Key = s.Key,
                    ResumeEventName = s.ResumeEventName
                })
                .ToList();
        }

        if (allowedEvents is null && waitKey is null && subscriptions is null)
            return null;

        return new WaitNodeGraphMetadata(waitKey, allowedEvents, subscriptions);
    }

    private static string ResolveNodeType(CompiledWorkflowDefinition def, string stateName)
    {
        // Wait は InitialState でも Resume 対象のため Start より優先する。
        if (def.WaitEventRouteTable.ContainsKey(stateName))
            return "Wait";
        if (string.Equals(def.InitialState, stateName, StringComparison.OrdinalIgnoreCase))
            return "Start";
        if (def.JoinTable.ContainsKey(stateName))
            return "Join";
        if (def.ForkTable.ContainsKey(stateName))
            return "Fork";
        if (def.Transitions.TryGetValue(stateName, out var byFact)
            && byFact.Values.Any(target => target.End))
            return "End";
        return "Task";
    }
}
