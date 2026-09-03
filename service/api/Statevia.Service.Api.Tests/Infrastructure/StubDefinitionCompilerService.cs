using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>IDefinitionCompilerService のテスト用スタブ。</summary>
internal sealed class StubDefinitionCompilerService : IDefinitionCompilerService
{
    private readonly (CompiledWorkflowDefinition Compiled, string CompiledJson) _result;
    private readonly Exception? _restoreException;

    public StubDefinitionCompilerService((CompiledWorkflowDefinition Compiled, string CompiledJson) result) =>
        _result = result;

    public StubDefinitionCompilerService(
        (CompiledWorkflowDefinition Compiled, string CompiledJson) result,
        Exception restoreException)
    {
        _result = result;
        _restoreException = restoreException;
    }

    public (CompiledWorkflowDefinition Compiled, string CompiledJson) ValidateAndCompile(
        string name,
        string yaml,
        Guid? tenantId = null) =>
        _result;

    public CompiledWorkflowDefinition RestoreFromStoredVersion(string sourceYaml, string compiledJson)
    {
        if (_restoreException is { } restoreException)
            throw restoreException;

        return _result.Compiled;
    }
}
