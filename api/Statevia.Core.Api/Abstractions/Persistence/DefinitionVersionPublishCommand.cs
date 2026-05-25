namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>定義版 publish の入力。</summary>
internal sealed record DefinitionVersionPublishCommand(
    Guid TenantInternalId,
    Guid DefinitionId,
    string Name,
    string SourceYaml,
    string CompiledJson,
    Guid NewVersionId);
