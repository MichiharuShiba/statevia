namespace Statevia.Service.Api.Persistence;

/// <summary>workflow_definitions テーブル。</summary>
internal class WorkflowDefinitionRow
{
    public Guid DefinitionId { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string SourceYaml { get; set; }
    public required string CompiledJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
