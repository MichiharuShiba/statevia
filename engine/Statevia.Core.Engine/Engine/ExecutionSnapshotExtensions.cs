using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// ExecutionInstance を ExecutionSnapshot に変換する拡張メソッドを提供します。
/// </summary>
public static class ExecutionSnapshotExtensions
{
    /// <summary>ワークフローインスタンスのスナップショットを取得します。</summary>
    public static ExecutionSnapshot ToSnapshot(this ExecutionInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new ExecutionSnapshot
        {
            ExecutionId = instance.ExecutionId,
            WorkflowName = instance.Definition.Name,
            ActiveStates = instance.GetActiveStates(),
            IsCompleted = instance.IsCompleted,
            IsCancelled = instance.IsCancelled,
            IsFailed = instance.IsFailed
        };
    }
}
