using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// コンパイル済み定義とワークフロー ID から <see cref="WorkflowInstance"/> を構築する境界。
/// </summary>
public interface IWorkflowInstanceFactory
{
    /// <summary>
    /// 実行用の <see cref="WorkflowInstance"/> を生成する。
    /// </summary>
    /// <param name="definition">コンパイル済みワークフロー定義。</param>
    /// <param name="workflowId">エンジン内キー（通常は GUID 既定書式）。</param>
    WorkflowInstance Create(CompiledWorkflowDefinition definition, string workflowId);
}
