using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// ExecutionInstance の状態出力を IReadOnlyStateStore として公開します。
/// Join 状態が依存状態の出力を参照する際に使用します。
/// </summary>
public sealed class ExecutionStateStore : IReadOnlyStateStore
{
    private readonly ExecutionInstance _instance;

    /// <summary>
    /// 指定インスタンスの状態出力を参照するストアを構築する。
    /// </summary>
    /// <param name="instance">対象のワークフローインスタンス。</param>
    public ExecutionStateStore(ExecutionInstance instance) => _instance = instance;

    /// <inheritdoc />
    public bool TryGetOutput(string stateName, out object? output) => _instance.TryGetOutput(stateName, out output);
}
