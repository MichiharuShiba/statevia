using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// ワークフローのユースケース（開始・一覧・取得・イベント・投影更新など）。
/// </summary>
public interface IWorkflowService
{
    /// <summary>ワークフローを開始し、表示用 ID 付きの応答を返す。</summary>
    Task<WorkflowResponse> StartAsync(
        string tenantId,
        StartWorkflowRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>
    /// ページング一覧を返す。クエリの status・definitionId・name 等で絞り込む。<paramref name="query"/>.<see cref="WorkflowListQuery.Limit"/> は必須。
    /// </summary>
    Task<PagedResult<WorkflowResponse>> ListPagedAsync(
        string tenantId,
        WorkflowListQuery query,
        CancellationToken ct);

    /// <summary>単一取得（一覧 <see cref="WorkflowResponse"/> と同一形。UI の WorkflowDTO 向け）。</summary>
    Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct);

    /// <summary>指定ワークフローがテナントに存在することを検証する（存在しなければ例外）。</summary>
    Task EnsureWorkflowExistsAsync(string tenantId, Guid workflowId, CancellationToken ct);

    /// <summary>投影済み実行グラフ JSON を返す。</summary>
    Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct);

    /// <summary>スナップショット行からグラフ JSON を取得する。無ければ <see langword="null"/>。</summary>
    Task<string?> TryGetSnapshotGraphJsonByWorkflowIdAsync(Guid workflowId, CancellationToken ct);

    /// <summary>現在のワークフロービュー（UI 向け DTO）を返す。</summary>
    Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct);

    /// <summary>指定シーケンス時点に近いワークフロービューを返す（リプレイ近似）。</summary>
    Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct);

    /// <summary>event_store 由来のタイムラインイベントを返す。</summary>
    Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct);

    /// <summary>Wait ノードを <paramref name="resumeKey"/> で再開する（内部でイベント発行）。</summary>
    Task ResumeNodeAsync(
        string tenantId,
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>ワークフローをキャンセル要求状態にし、エンジンへ伝播する。</summary>
    Task CancelAsync(
        string tenantId,
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>名前付きイベントをエンジンへ発行する。</summary>
    Task PublishEventAsync(
        string tenantId,
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>
    /// エンジンの現在状態を読み取り、投影（workflows / execution_graph_snapshots）を更新する。
    /// </summary>
    Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct);
}
