using Statevia.Core.Api.Contracts;
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
    Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct);
    Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct);
    Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct);
}
