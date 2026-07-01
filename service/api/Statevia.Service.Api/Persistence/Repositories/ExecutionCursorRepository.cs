using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Persistence;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Persistence.Repositories;

/// <summary>execution_cursors 永続化。</summary>
internal sealed class ExecutionCursorRepository : IExecutionCursorRepository
{
    /// <inheritdoc />
    public async Task UpsertAsync(ICoreUnitOfWork uow, ExecutionCursorRow row, CancellationToken ct)
    {
        var existing = await uow.Db.ExecutionCursors
            .FirstOrDefaultAsync(x => x.ExecutionId == row.ExecutionId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            uow.Db.ExecutionCursors.Add(row);
            return;
        }

        existing.TenantId = row.TenantId;
        existing.CurrentNodeId = row.CurrentNodeId;
        existing.CurrentRuntimeId = row.CurrentRuntimeId;
        existing.CurrentWorkerId = row.CurrentWorkerId;
        existing.State = row.State;
        existing.UpdatedAt = row.UpdatedAt;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct)
    {
        var existing = await uow.Db.ExecutionCursors
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
            uow.Db.ExecutionCursors.Remove(existing);
    }

    /// <inheritdoc />
    public Task<ExecutionCursorRow?> GetByExecutionIdAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct) =>
        uow.Db.ExecutionCursors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);
}
