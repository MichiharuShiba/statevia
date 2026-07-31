namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行ランタイムチェックポイントの文書ストア（キー = executionId）。
/// </summary>
/// <remarks>
/// <para>
/// Application は本ポートのみに依存する。実装は PostgreSQL テーブルでも、
/// MongoDB / DynamoDB 等のドキュメントストアでも差し替え可能とする。
/// </para>
/// <para>
/// 現行の Postgres 実装は物理テーブル <c>execution_runtime_checkpoints</c> を用いるが、
/// 契約上は RDB 行ではなく文書（document）として扱う。
/// 将来ドキュメント DB へ移す場合は同一 ACID tx 参加を前提にせず、
/// Outbox / 結果整合など別パターンで投影系と整合させる。
/// </para>
/// </remarks>
public interface IExecutionCheckpointStore
{
    /// <summary>チェックポイント文書を upsert する。</summary>
    /// <param name="uow">同一トランザクションの Unit of Work（現行 Postgres 実装向け。切替時は実装側で無視可）。</param>
    /// <param name="document">保存するチェックポイント文書。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task UpsertAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointDocument document,
        CancellationToken ct);

    /// <summary>実行 ID でチェックポイント文書を取得する。</summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">実行 ID（文書キー）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>文書。存在しなければ null。</returns>
    Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);

    /// <summary>チェックポイント文書を削除する。</summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">実行 ID（文書キー）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task DeleteAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);
}
