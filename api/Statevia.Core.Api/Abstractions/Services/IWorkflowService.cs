using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

public interface IWorkflowService
{
    Task<WorkflowResponse> StartAsync(
        string tenantId,
        StartWorkflowRequest request,
        string? idempotencyKey,
        string method,
        string path,
        CancellationToken ct);
    Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct);
    Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct);
}
