using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface IWorkflowRepository
{
    Task<WorkflowRow?> GetByIdAsync(string tenantId, Guid workflowId, CancellationToken ct);
    Task<List<(WorkflowRow Workflow, string? DisplayId)>> ListWithDisplayIdsAsync(string tenantId, CancellationToken ct);
    Task AddWorkflowAndSnapshotAsync(WorkflowRow workflow, ExecutionGraphSnapshotRow snapshot, CancellationToken ct);
    Task<ExecutionGraphSnapshotRow?> GetSnapshotByWorkflowIdAsync(Guid workflowId, CancellationToken ct);
    Task UpdateWorkflowAndSnapshotAsync(Guid workflowId, string status, bool? cancelRequested, string graphJson, CancellationToken ct);
}
