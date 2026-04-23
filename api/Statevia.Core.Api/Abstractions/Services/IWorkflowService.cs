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
    /// <summary>ページング。status および任意の definition（display/UUID 解決）、<paramref name="nameContains"/>（displayId 部分一致または workflow UUID 完全一致）。</summary>
    Task<PagedResult<WorkflowResponse>> ListPagedAsync(
        string tenantId,
        int offset,
        int limit,
        string? status,
        string? definitionId,
        string? nameContains,
        CancellationToken ct);
    /// <summary>単一取得（一覧 <see cref="WorkflowResponse"/> と同一形。UI の WorkflowDTO 向け）。</summary>
    Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct);
    Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct);
    Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct);
    Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct);
    Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct);

    /// <summary>
    /// エンジンの現在状態を読み取り、投影（workflows / execution_graph_snapshots）を更新する。
    /// </summary>
    Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct);
}
