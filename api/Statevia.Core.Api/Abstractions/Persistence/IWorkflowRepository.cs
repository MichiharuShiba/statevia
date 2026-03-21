using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface IWorkflowRepository
{
    Task<WorkflowRow?> GetByIdAsync(string tenantId, Guid workflowId, CancellationToken ct);
    Task<List<(WorkflowRow Workflow, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct);
    Task AddWorkflowAndSnapshotAsync(WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct);
    /// <summary>同一 <see cref="CoreDbContext"/> 上に追加のみ（SaveChanges は呼び出し側）。</summary>
    Task AddWorkflowAndSnapshotAsync(CoreDbContext db, WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct);
    Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(Guid workflowId, CancellationToken ct);
    Task UpdateWorkflowAndSnapshotAsync(Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct);
    /// <summary>同一 <see cref="CoreDbContext"/> 上で更新のみ（SaveChanges は呼び出し側）。</summary>
    Task UpdateWorkflowAndSnapshotAsync(CoreDbContext db, Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct);
}
