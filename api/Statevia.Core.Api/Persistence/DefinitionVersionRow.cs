namespace Statevia.Core.Api.Persistence;

/// <summary>definition_versions テーブル（immutable 定義版の truth）。</summary>
internal class DefinitionVersionRow
{
    public Guid DefinitionVersionId { get; set; }

    public Guid DefinitionId { get; set; }

    public int Version { get; set; }

    public required string SourceYaml { get; set; }

    public required string CompiledJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
