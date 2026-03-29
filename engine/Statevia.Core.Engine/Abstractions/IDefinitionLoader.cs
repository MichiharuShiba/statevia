using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// YAML / JSON 文字列から <see cref="WorkflowDefinition"/> を構築する。
/// Core-API は複数実装（states / nodes 等）をストラテジで切り替える。
/// </summary>
public interface IDefinitionLoader
{
    /// <summary>コンテンツをパースしてワークフロー定義を返す。</summary>
    /// <exception cref="ArgumentException">形式が不正な場合。</exception>
    /// <exception cref="InvalidOperationException">パースに失敗した場合。</exception>
    WorkflowDefinition Load(string content);
}
