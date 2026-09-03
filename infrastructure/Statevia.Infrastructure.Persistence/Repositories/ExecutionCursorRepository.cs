using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>execution_cursors 永続化。</summary>
/// <remarks>
/// <para>
/// 投影キューの途中グラフ書き込みと Wait checkpoint 永続化が、同じ <c>execution_id</c> の cursor を並行して書く。
/// SELECT 後 INSERT だと QueryFilter 見逃しや PK 競合（PostgreSQL 23505）で Wait 行まで巻き戻る。
/// </para>
/// <para>
/// upsert は QueryFilter を経由せず <c>INSERT ... ON CONFLICT (execution_id) DO UPDATE</c> で 1 行に収束する。
/// 読み取り（<see cref="GetByExecutionIdAsync"/>）の HasQueryFilter は維持する。
/// </para>
/// </remarks>
internal sealed class ExecutionCursorRepository : IExecutionCursorRepository
{
    /// <inheritdoc />
    public async Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct)
    {
        var db = uow.GetDb();
        var local = db.ExecutionCursors.Local.FirstOrDefault(x => x.ExecutionId == row.ExecutionId);
        if (local is not null)
            db.Entry(local).State = EntityState.Detached;

        FormattableString command =
            $"""
            INSERT INTO execution_cursors (
                execution_id,
                tenant_id,
                current_node_id,
                current_runtime_id,
                current_worker_id,
                state,
                updated_at)
            VALUES (
                {row.ExecutionId},
                {row.TenantId},
                {row.CurrentNodeId},
                {row.CurrentRuntimeId},
                {row.CurrentWorkerId},
                {row.State},
                {row.UpdatedAt})
            ON CONFLICT (execution_id) DO UPDATE SET
                tenant_id = EXCLUDED.tenant_id,
                current_node_id = EXCLUDED.current_node_id,
                current_runtime_id = EXCLUDED.current_runtime_id,
                current_worker_id = EXCLUDED.current_worker_id,
                state = EXCLUDED.state,
                updated_at = EXCLUDED.updated_at
            """;

        await db.Database.ExecuteSqlInterpolatedAsync(command, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct)
    {
        var existing = await uow.GetDb().ExecutionCursors
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
            uow.GetDb().ExecutionCursors.Remove(existing);
    }

    /// <inheritdoc />
    public Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.GetDb().ExecutionCursors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);
}
