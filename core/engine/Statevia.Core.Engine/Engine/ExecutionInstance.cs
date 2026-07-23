using System.Collections.Concurrent;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.ExecutionGraphs;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 つのワークフローインスタンスの実行状態を保持します。
/// FSM、JoinTracker、ExecutionGraph、状態出力、アクティブ状態などを管理します。
/// </summary>
public sealed class ExecutionInstance
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

    /// <summary>end 到達で完了したか。</summary>
    public bool IsCompleted { get; private set; }
    /// <summary>協調的キャンセルで停止したか。</summary>
    public bool IsCancelled { get; private set; }
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
    public void MarkCancelled() => IsCancelled = true;

    /// <summary>失敗により停止したとマークする。</summary>
    public void MarkFailed() => IsFailed = true;

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
}
