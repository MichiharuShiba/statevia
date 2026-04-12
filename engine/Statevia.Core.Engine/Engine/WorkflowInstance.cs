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
public sealed class WorkflowInstance
{
    /// <summary>ワークフローインスタンス ID。</summary>
    public required string WorkflowId { get; init; }
    /// <summary>コンパイル済み定義。</summary>
    public required CompiledWorkflowDefinition Definition { get; init; }
    /// <summary>事実駆動 FSM。</summary>
    public required IFsm Fsm { get; init; }
    /// <summary>Join トラッカー。</summary>
    public required IJoinTracker JoinTracker { get; init; }
    /// <summary>実行グラフ（観測用）。</summary>
    public required ExecutionGraph Graph { get; init; }

    private readonly Dictionary<string, object?> _stateOutputs = new(StringComparer.OrdinalIgnoreCase);
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

    public void SetOutput(string stateName, object? output) { lock (_lock) { _stateOutputs[stateName] = output; } }
    public bool TryGetOutput(string stateName, out object? output) { lock (_lock) { return _stateOutputs.TryGetValue(stateName, out output); } }
    public void AddActiveState(string stateName) { lock (_lock) { _activeStates.Add(stateName); } }
    public void RemoveActiveState(string stateName) { lock (_lock) { _activeStates.Remove(stateName); } }
    public IReadOnlyList<string> GetActiveStates() { lock (_lock) { return _activeStates.ToList(); } }
    public void MarkCompleted() => IsCompleted = true;
    public void MarkCancelled() => IsCancelled = true;
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
