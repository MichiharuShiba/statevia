using Statevia.Core.Abstractions;

namespace Statevia.Core.Engine;

/// <summary>
/// WorkflowInstance の状態出力を IReadOnlyStateStore として公開します。
/// Join 状態が依存状態の出力を参照する際に使用します。
/// </summary>
public sealed class WorkflowStateStore : IReadOnlyStateStore
{
    private readonly WorkflowInstance _instance;

    public WorkflowStateStore(WorkflowInstance instance) => _instance = instance;

    /// <inheritdoc />
    public bool TryGetOutput(string stateName, out object? output) => _instance.TryGetOutput(stateName, out output);
}
