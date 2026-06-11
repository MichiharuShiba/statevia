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
    /// <param name="tenantId">Visibility 検証用テナント UUID（未指定時はスキップ）。</param>
    /// <returns>コンパイル済み定義とその JSON 文字列。</returns>
    (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml, Guid? tenantId = null);

    /// <summary>
    /// 保存済み版（compiled_json + 同版 source_yaml）から Engine 投入用定義を復元する。
    /// </summary>
    /// <param name="sourceYaml">当該版の source_yaml。</param>
    /// <param name="compiledJson">当該版の compiled_json。</param>
    CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson);
}
