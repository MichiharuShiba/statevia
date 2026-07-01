namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>定義版 publish の入力。</summary>
public sealed record DefinitionVersionPublishCommand(
    Guid TenantId,
    Guid DefinitionId,
    string Name,
    string SourceYaml,
    string CompiledJson,
    Guid NewVersionId);
