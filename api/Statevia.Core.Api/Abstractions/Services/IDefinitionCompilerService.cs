using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。</summary>
public interface IDefinitionCompilerService
{
    /// <summary>
    /// 定義名と YAML を検証し、コンパイル結果と JSON 表現を返す。
    /// </summary>
    /// <param name="name">定義名。</param>
    /// <param name="yaml">定義ソース YAML。</param>
    /// <returns>コンパイル済み定義とその JSON 文字列。</returns>
    (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml);
}
