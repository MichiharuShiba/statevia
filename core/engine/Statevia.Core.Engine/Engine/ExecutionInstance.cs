using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;
using System.Collections.Concurrent;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 つのワークフローインスタンスの実行状態を保持します。
/// FSM、JoinTracker、ExecutionGraph、状態出力、アクティブ状態などを管理します。
/// </summary>
/// <remarks>
/// <para>実行中 Action にはインスタンスの Action CTS を渡す。<see cref="MarkCancelled"/> と <see cref="Dispose"/> がそのトークンをキャンセルする。</para>
/// </remarks>
public sealed class ExecutionInstance : IDisposable
{
    /// <summary>ワークフローインスタンス ID。</summary>
    public required string ExecutionId { get; init; }
    /// <summary>コンパイル済み定義。</summary>
    public required CompiledWorkflowDefinition Definition { get; init; }
    /// <summary>事実駆動 FSM。</summary>
    public required IFsm Fsm { get; init; }
    /// <summary>Join トラッカー。</summary>
    public required IJoinTracker JoinTracker { get; init; }
    /// <summary>実行グラフ（観測用）。</summary>
    public required ExecutionGraph Graph { get; init; }

    /// <summary>実行時データの Execution Context（パス評価根）。</summary>
    public WorkflowExecutionContext Context { get; private set; } = WorkflowExecutionContext.Create(null);

    private readonly Dictionary<string, object?> _stateOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _stateAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Guid, byte> _appliedPublishClientEventIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _appliedCancelClientEventIds = new();
    private readonly CancellationTokenSource _actionCts = new();

    /// <summary>end 到達で完了したか。</summary>
    public bool IsCompleted { get; private set; }
    /// <summary>協調的キャンセルで停止したか。</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>実行中 Action に渡す協調 Cancel 用トークン。Unload 後は破棄済み。</summary>
    internal CancellationToken ActionCancellationToken => _actionCts.Token;
    /// <summary>失敗で停止したか。</summary>
    public bool IsFailed { get; private set; }

    /// <summary>開始 input と実行メタデータで Context を初期化する（実行開始時に 1 回）。</summary>
    /// <param name="input"><see cref="IExecutionEngine.Start"/> に渡された開始 input。</param>
    public void InitializeContext(object? input)
    {
        Context = WorkflowExecutionContext.Create(input, ExecutionId, Definition.Name);
    }

    /// <summary>
    /// 状態完了時の出力を記録し、Context の <c>states</c> を更新する。
    /// <see cref="CompiledWorkflowDefinition.StateOutputs"/> があれば <c>vars</c> にも代入する。
    /// </summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="output"><see cref="IStateExecutor.ExecuteAsync"/> の戻り値。</param>
    public void SetOutput(string stateName, object? output)
    {
        lock (_lock)
        {
            _stateOutputs[stateName] = output;
            Context.SetStateOutput(stateName, output);
            if (Definition.StateOutputs.TryGetValue(stateName, out var varsPath))
            {
                Context.SetVar(varsPath, output);
            }
        }
    }

    /// <summary>指定状態の出力を取得する。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="output">取得した出力。存在しないときは既定値。</param>
    /// <returns>出力が存在するとき true。</returns>
    public bool TryGetOutput(string stateName, out object? output) { lock (_lock) { return _stateOutputs.TryGetValue(stateName, out output); } }

    /// <summary>状態の試行回数をインクリメントし、次の試行番号（1 始まり）を返す。</summary>
    /// <param name="stateName">状態名。</param>
    /// <returns>次の試行番号。</returns>
    public int NextAttempt(string stateName)
    {
        lock (_lock)
        {
            var next = _stateAttempts.TryGetValue(stateName, out var current) ? current + 1 : 1;
            _stateAttempts[stateName] = next;
            return next;
        }
    }

    /// <summary>現在アクティブな状態集合へ追加する。</summary>
    /// <param name="stateName">状態名。</param>
    public void AddActiveState(string stateName) { lock (_lock) { _activeStates.Add(stateName); } }

    /// <summary>現在アクティブな状態集合から除去する。</summary>
    /// <param name="stateName">状態名。</param>
    public void RemoveActiveState(string stateName) { lock (_lock) { _activeStates.Remove(stateName); } }

    /// <summary>現在アクティブな状態名のスナップショットを返す。</summary>
    /// <returns>アクティブ状態名の一覧。</returns>
    public IReadOnlyList<string> GetActiveStates() { lock (_lock) { return _activeStates.ToList(); } }

    /// <summary>ワークフローを正常終了としてマークする。</summary>
    /// <param name="terminalOutput">終端 State の output（Context.output に設定）。</param>
    public void MarkCompleted(object? terminalOutput = null)
    {
        Context.SetWorkflowOutput(terminalOutput);
        IsCompleted = true;
    }

    /// <summary>協調的キャンセルにより停止したとマークする。</summary>
    /// <summary>協調的キャンセルにより停止したとマークし、実行中 Action の CT をキャンセルする。</summary>
    public void MarkCancelled()
    {
        IsCancelled = true;
        try
        {
            _actionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Unload 後の二重 Cancel はフラグのみ
        }
    }

    /// <summary>失敗により停止したとマークする。</summary>
    public void MarkFailed() => IsFailed = true;

    /// <summary>
    /// Unload 時に Action 用 <see cref="CancellationTokenSource"/> をキャンセルして破棄する。
    /// </summary>
    /// <remarks>二重呼び出しは CTS 破棄済みとして無視する。</remarks>
    /// <summary>
    /// Action 用 <see cref="CancellationTokenSource"/> をキャンセルして破棄する。
    /// </summary>
    /// <remarks>Unload / Engine Dispose から呼ぶ。二重呼び出しは CTS 破棄済みとして無視する。</remarks>
    public void Dispose()
    {
        try
        {
            _actionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _actionCts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <paramref name="clientEventId"/> を Publish 冪等集合へ未登録なら登録し true。既登録なら false。
    /// </summary>
    internal bool TryRegisterPublishClientEventId(Guid clientEventId) =>
        _appliedPublishClientEventIds.TryAdd(clientEventId, 0);

    /// <summary>Publish 冪等集合から <paramref name="clientEventId"/> を取り除く（発行失敗時の巻き戻し用）。</summary>
    internal void RemovePublishClientEventId(Guid clientEventId) =>
        _appliedPublishClientEventIds.TryRemove(clientEventId, out _);

    /// <summary>
    /// <paramref name="clientEventId"/> を Cancel 冪等集合へ未登録なら登録し true。既登録なら false。
    /// </summary>
    internal bool TryRegisterCancelClientEventId(Guid clientEventId) =>
        _appliedCancelClientEventIds.TryAdd(clientEventId, 0);

    /// <summary>チェックポイント用にランタイム状態をエクスポートする。</summary>
    internal ExecutionRuntimeCheckpoint ExportRuntimeCheckpoint()
    {
        lock (_lock)
        {
            var pendingWaits = Graph.GetNodesSnapshot()
                .Where(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                    && n.CompletedAt is null)
                .Select(n => new CheckpointPendingWait
                {
                    NodeId = n.NodeId,
                    NodeName = n.NodeName,
                    AllowedEvents = n.AllowedEvents?.ToList() ?? [],
                    Subscriptions = n.Subscriptions?
                        .Select(s => new CheckpointWaitSubscription
                        {
                            Topic = s.Topic,
                            Key = s.Key,
                            ResumeEventName = s.ResumeEventName
                        })
                        .ToList()
                })
                .ToList();

            var joinData = JoinTracker is JoinTracker concreteJoin
                ? concreteJoin.ExportCheckpoint()
                : new CheckpointJoinData
                {
                    JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(),
                    JoinSourceNodeIds = new Dictionary<string, IReadOnlyDictionary<string, string>>(),
                    StartedJoins = []
                };

            return new ExecutionRuntimeCheckpoint
            {
                SchemaVersion = ExecutionRuntimeCheckpoint.CurrentSchemaVersion,
                ExecutionId = ExecutionId,
                DefinitionName = Definition.Name,
                IsCompleted = IsCompleted,
                IsCancelled = IsCancelled,
                IsFailed = IsFailed,
                ActiveStates = _activeStates.ToList(),
                StateAttempts = new Dictionary<string, int>(_stateAttempts, StringComparer.OrdinalIgnoreCase),
                StateOutputs = _stateOutputs.ToDictionary(
                    kv => kv.Key,
                    kv => CheckpointJson.ToElement(kv.Value),
                    StringComparer.OrdinalIgnoreCase),
                AppliedPublishClientEventIds = _appliedPublishClientEventIds.Keys.ToList(),
                AppliedCancelClientEventIds = _appliedCancelClientEventIds.Keys.ToList(),
                Context = Context.ExportCheckpoint(),
                Graph = Graph.ExportCheckpoint(),
                Join = joinData,
                PendingWaits = pendingWaits
            };
        }
    }

    /// <summary>チェックポイントから可変ランタイム状態を適用する（Definition / Fsm / Graph 器は既存）。</summary>
    /// <param name="checkpoint">チェックポイント。</param>
    internal void ApplyRuntimeCheckpoint(ExecutionRuntimeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        lock (_lock)
        {
            IsCompleted = checkpoint.IsCompleted;
            IsCancelled = checkpoint.IsCancelled;
            IsFailed = checkpoint.IsFailed;
            _activeStates.Clear();
            foreach (var state in checkpoint.ActiveStates)
            {
                _activeStates.Add(state);
            }

            _stateAttempts.Clear();
            foreach (var (name, attempt) in checkpoint.StateAttempts)
            {
                _stateAttempts[name] = attempt;
            }

            _stateOutputs.Clear();
            foreach (var (name, output) in checkpoint.StateOutputs)
            {
                _stateOutputs[name] = CheckpointJson.FromElement(output);
            }

            _appliedPublishClientEventIds.Clear();
            foreach (var id in checkpoint.AppliedPublishClientEventIds)
            {
                _appliedPublishClientEventIds[id] = 0;
            }

            _appliedCancelClientEventIds.Clear();
            foreach (var id in checkpoint.AppliedCancelClientEventIds)
            {
                _appliedCancelClientEventIds[id] = 0;
            }

            Context = WorkflowExecutionContext.CreateFromCheckpoint(
                checkpoint.Context,
                ExecutionId,
                Definition.Name);
            Graph.ImportFromCheckpoint(checkpoint.Graph);
            if (JoinTracker is JoinTracker concreteJoin)
            {
                concreteJoin.ImportFromCheckpoint(checkpoint.Join);
            }
        }
    }
}
