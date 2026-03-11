namespace Statevia.Core.Api.Persistence;

/// <summary>workflow_definitions テーブル。</summary>
public class WorkflowDefinitionRow
{
    public Guid DefinitionId { get; set; }
    public required string Name { get; set; }
    public required string SourceYaml { get; set; }
    public required string CompiledJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
