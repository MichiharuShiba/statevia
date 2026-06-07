using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// 実行インスタンスのユースケース（開始・一覧・取得・イベント・投影更新など）。
/// </summary>
public interface IExecutionService
{
    /// <summary>実行を開始し、表示用 ID 付きの応答を返す。</summary>
    Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>
    /// ページング一覧を返す。クエリの status・definitionId・name 等で絞り込む。<paramref name="query"/>.<see cref="ExecutionListQuery.Limit"/> は必須。
    /// </summary>
    Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListQuery query,
        CancellationToken ct);

    /// <summary>単一取得（一覧 <see cref="ExecutionResponse"/> と同一形。UI の ExecutionDTO 向け）。</summary>
    Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct);

    /// <summary>指定 execution がテナントに存在することを検証する（存在しなければ例外）。</summary>
    Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct);

    /// <summary>投影済み実行グラフ JSON を返す。</summary>
    Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct);

    /// <summary>スナップショット行からグラフ JSON を取得する。無ければ <see langword="null"/>。</summary>
    Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct);

    /// <summary>現在の実行ビュー（UI 向け DTO）を返す。</summary>
    Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct);

    /// <summary>指定シーケンス時点に近い実行ビューを返す（リプレイ近似）。</summary>
    Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct);

    /// <summary>event_store 由来のタイムラインイベントを返す。</summary>
    Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct);

    /// <summary>Wait ノードを <paramref name="resumeKey"/> で再開する（内部でイベント発行）。</summary>
    Task ResumeNodeAsync(
        string idOrUuid,
        string nodeId,
        string? resumeKey,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>実行をキャンセル要求状態にし、エンジンへ伝播する。</summary>
    Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>名前付きイベントをエンジンへ発行する。</summary>
    Task PublishEventAsync(
        string idOrUuid,
        string eventName,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct);

    /// <summary>
    /// エンジンの現在状態を読み取り、投影（executions / execution_graph_snapshots）を更新する。
    /// </summary>
    Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct);
}
