namespace Statevia.Core.Api.Persistence;

/// <summary>executions テーブル（projection）。</summary>
internal class ExecutionRow
{
    public Guid ExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DefinitionId { get; set; }

    /// <summary>開始時に固定した定義版（definition_versions FK）。</summary>
    public Guid DefinitionVersionId { get; set; }

    public required string Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CancelRequested { get; set; }
    public bool RestartLost { get; set; }
}
