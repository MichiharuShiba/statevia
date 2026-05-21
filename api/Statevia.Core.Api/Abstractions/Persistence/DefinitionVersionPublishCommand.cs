namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>定義版 publish の入力。</summary>
internal sealed record DefinitionVersionPublishCommand(
    string TenantId,
    Guid DefinitionId,
    string Name,
    string SourceYaml,
    string CompiledJson,
    Guid NewVersionId);
