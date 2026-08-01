using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL 上の実行チェックポイント文書ストア実装。
/// </summary>
/// <remarks>
/// 物理テーブルは <c>execution_runtime_checkpoints</c>。
/// Application 契約 <see cref="IExecutionCheckpointStore"/> のアダプタであり、
/// 将来ドキュメント DB 実装へ差し替える際の参照実装とする。
/// </remarks>
internal sealed class PostgresExecutionCheckpointStore : IExecutionCheckpointStore
{
    /// <inheritdoc />
    public async Task UpsertAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointDocument document,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(x => x.ExecutionId == document.ExecutionId, ct)
            .ConfigureAwait(false);
        if (existing is null)
        {
            db.ExecutionCheckpoints.Add(document);
            return;
        }

        existing.CheckpointJson = document.CheckpointJson;
        existing.SchemaVersion = document.SchemaVersion;
        existing.UpdatedAt = document.UpdatedAt;
    }

    /// <inheritdoc />
    public Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct) =>
        uow.GetDb().ExecutionCheckpoints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    /// <inheritdoc />
    public async Task DeleteAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct)
    {
        var documents = await uow.GetDb().ExecutionCheckpoints
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (documents.Count == 0)
            return;

        uow.GetDb().ExecutionCheckpoints.RemoveRange(documents);
    }
}
