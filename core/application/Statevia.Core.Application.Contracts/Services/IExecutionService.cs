using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Core.Application.Contracts.Services;

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
    /// 受理済み実行の Start work item を同一プロセスで実行する（Worker 用）。
    /// </summary>
    Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct);

    /// <summary>
    /// ページング一覧を返す。クエリの status・definitionId・name 等で絞り込む。
    /// </summary>
    Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListPageQuery query,
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

    /// <summary>
    /// 所有喪失後の継続再開（checkpoint hydrate）。Wait イベントは消費しない。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="requestContext">コマンド文脈（Worker 等）。</param>
    /// <param name="ct">キャンセル。</param>
    Task RecoverExecutionAsync(
        Guid executionId,
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

    /// <summary>
    /// Wait 登録直後の suspend 通知。チェックポイントを保存してエンジンから Unload する。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="nodeId">登録された Wait ノード ID（ログ用）。</param>
    /// <param name="ct">キャンセル。</param>
    Task PersistCheckpointAndUnloadAsync(Guid executionId, string nodeId, CancellationToken ct);

    /// <summary>
    /// Engine 実行 ID 文字列を正としてチェックポイントを保存し Unload する。
    /// </summary>
    /// <param name="engineExecutionId">Engine 辞書キー（Start 時に渡した文字列）。</param>
    /// <param name="nodeId">登録された Wait ノード ID（ログ用）。</param>
    /// <param name="ct">キャンセル。</param>
    Task PersistCheckpointAndUnloadByEngineIdAsync(
        string engineExecutionId,
        string nodeId,
        CancellationToken ct);

    /// <summary>
    /// ステップ完了時に checkpoint を保存する（Unload しない）。
    /// </summary>
    /// <param name="engineExecutionId">Engine 辞書キー。</param>
    /// <param name="ct">キャンセル。</param>
    Task PersistCheckpointKeepLoadedAsync(string engineExecutionId, CancellationToken ct);

    /// <summary>
    /// Worker セッションの所有を獲得しトラッカーへ登録する。
    /// </summary>
    /// <returns>獲得後の世代。失敗時は null。</returns>
    Task<long?> BeginOwnedSessionAsync(
        Guid executionId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct);

    /// <summary>所有 lease を延長する（世代一致）。</summary>
    Task<bool> RenewOwnedSessionLeaseAsync(
        Guid executionId,
        TimeSpan leaseDuration,
        CancellationToken ct);

    /// <summary>所有を解放しトラッカーから削除する。</summary>
    Task EndOwnedSessionAsync(Guid executionId, CancellationToken ct);

    /// <summary>
    /// lease 喪失時にローカル Engine を Unload しトラッカーを捨てる（DB 所有は触らない）。
    /// </summary>
    Task AbandonLocalOwnedSessionAsync(Guid executionId);

    /// <summary>
    /// ローカル Engine の Load が Wait Unload または終端になるまで待つ（Worker の 1 claim = 1 Load）。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    Task AwaitLocalExecutionLoadAsync(Guid executionId, CancellationToken ct);
}
