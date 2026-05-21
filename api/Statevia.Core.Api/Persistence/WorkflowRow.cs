namespace Statevia.Core.Api.Persistence;

/// <summary>workflows テーブル（projection）。</summary>
internal class WorkflowRow
{
    public Guid WorkflowId { get; set; }
    public string TenantId { get; set; } = "default";
    public Guid DefinitionId { get; set; }

    /// <summary>開始時に固定した定義版（definition_versions FK）。</summary>
    public Guid DefinitionVersionId { get; set; }

    public required string Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CancelRequested { get; set; }
    public bool RestartLost { get; set; }
}
