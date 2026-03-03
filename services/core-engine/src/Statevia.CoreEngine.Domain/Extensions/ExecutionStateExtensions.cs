using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Node;

namespace Statevia.CoreEngine.Domain.Extensions;

/// <summary>ExecutionState の拡張メソッド。</summary>
public static class ExecutionStateExtensions
{
    /// <summary>Cancel が既に要求されているか（cancelRequestedAt が設定済みか）。</summary>
    public static bool IsCancelRequested(this ExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.CancelRequestedAt is not null;
    }

    /// <summary>指定 nodeId のノードを newNode で置き換えた新しい ExecutionState を返す。存在しない場合は state をそのまま返す。</summary>
    public static ExecutionState WithNode(this ExecutionState state, string nodeId, NodeState newNode)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Nodes.ContainsKey(nodeId)) return state;
        var newNodes = new Dictionary<string, NodeState>(state.Nodes) { [nodeId] = newNode };
        return state with { Nodes = newNodes };
    }

    /// <summary>指定 nodeId のノードに update を適用した新しい ExecutionState を返す。存在しない場合は state をそのまま返す。</summary>
    public static ExecutionState WithNodeUpdated(this ExecutionState state, string nodeId, Func<NodeState, NodeState> update)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Nodes.TryGetValue(nodeId, out var current)) return state;
        var newNodes = new Dictionary<string, NodeState>(state.Nodes) { [nodeId] = update(current) };
        return state with { Nodes = newNodes };
    }
}
