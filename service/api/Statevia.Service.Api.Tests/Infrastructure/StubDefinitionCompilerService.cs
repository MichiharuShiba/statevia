using Statevia.Service.Api.Abstractions.Services;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>IDefinitionCompilerService のテスト用スタブ。</summary>
internal sealed class StubDefinitionCompilerService : IDefinitionCompilerService
{
    private readonly (CompiledWorkflowDefinition Compiled, string CompiledJson) _result;

    public StubDefinitionCompilerService((CompiledWorkflowDefinition Compiled, string CompiledJson) result) =>
        _result = result;

    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
        string name,
        string yaml,
        Guid? tenantId = null) =>
        _result;

    public CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson) =>
        _result.Compiled;
}
