using Statevia.Core.Abstractions;

namespace Statevia.Core.Engine;

/// <summary>
/// WorkflowInstance を WorkflowSnapshot に変換する拡張メソッドを提供します。
/// </summary>
public static class WorkflowSnapshotExtensions
{
    /// <summary>ワークフローインスタンスのスナップショットを取得します。</summary>
    public static WorkflowSnapshot ToSnapshot(this WorkflowInstance instance) => new()
    {
        WorkflowId = instance.WorkflowId,
        WorkflowName = instance.Definition.Name,
        ActiveStates = instance.GetActiveStates(),
        IsCompleted = instance.IsCompleted,
        IsCancelled = instance.IsCancelled,
        IsFailed = instance.IsFailed
    };
}
