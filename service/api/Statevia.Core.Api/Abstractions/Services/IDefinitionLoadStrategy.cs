using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// 定義 YAML の形式に応じて適切な <see cref="Statevia.Core.Engine.Abstractions.IDefinitionLoader"/> を選び、
/// <see cref="WorkflowDefinition"/> を返す。拡張時は別実装を DI で差し替え可能。
/// </summary>
public interface IDefinitionLoadStrategy
{
    /// <summary>コンテンツを解析し、対応するローダで <see cref="WorkflowDefinition"/> を構築する。</summary>
    WorkflowDefinition Load(string content);
}
