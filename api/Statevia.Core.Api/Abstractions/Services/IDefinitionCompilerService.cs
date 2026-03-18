using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>YAML を検証・コンパイルして CompiledWorkflowDefinition を返す。</summary>
public interface IDefinitionCompilerService
{
    (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(string name, string yaml);
}
