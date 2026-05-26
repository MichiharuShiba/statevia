using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// コンパイル済み定義とワークフロー ID から <see cref="ExecutionInstance"/> を構築する境界。
/// </summary>
public interface IExecutionInstanceFactory
{
    /// <summary>
    /// 実行用の <see cref="ExecutionInstance"/> を生成する。
    /// </summary>
    /// <param name="definition">コンパイル済みワークフロー定義。</param>
    /// <param name="executionId">エンジン内キー（通常は GUID 既定書式）。</param>
    ExecutionInstance Create(CompiledWorkflowDefinition definition, string executionId);
}
