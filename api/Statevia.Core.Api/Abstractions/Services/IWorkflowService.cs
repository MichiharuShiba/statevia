using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

public interface IWorkflowService
{
    Task<WorkflowResponse?> StartAsync(string tenantId, StartWorkflowRequest request, CommandDedupKey? dedupKey, CancellationToken ct);
    Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct);
    Task<string?> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<bool> CancelAsync(string tenantId, string idOrUuid, CommandDedupKey? dedupKey, CancellationToken ct);
    Task<bool> PublishEventAsync(string tenantId, string idOrUuid, string eventName, CommandDedupKey? dedupKey, CancellationToken ct);
}
