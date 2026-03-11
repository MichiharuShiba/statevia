namespace Statevia.Core.Api.Persistence;

/// <summary>workflows テーブル（projection）。</summary>
public class WorkflowRow
{
    public Guid WorkflowId { get; set; }
    public Guid DefinitionId { get; set; }
    public required string Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CancelRequested { get; set; }
    public bool RestartLost { get; set; }
}
