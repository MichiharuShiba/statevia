using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

/// <summary>
/// <see cref="IEventDeliveryDedupRepository"/> の EF Core 実装。
/// </summary>
internal sealed class EventDeliveryDedupRepository : IEventDeliveryDedupRepository
{
    /// <inheritdoc />
    public Task<EventDeliveryDedupRow?> FindAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        CancellationToken cancellationToken) =>
        uow.Db.EventDeliveryDedup.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowId == workflowId && x.ClientEventId == clientEventId,
                cancellationToken);

    /// <inheritdoc />
    public Task AddReceivedAsync(ICoreUnitOfWork uow, EventDeliveryDedupRow row, CancellationToken cancellationToken)
    {
        uow.Db.EventDeliveryDedup.Add(row);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        EventDeliveryDedupStatusUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var affected = await uow.Db.EventDeliveryDedup
            .Where(x => x.TenantId == tenantId && x.WorkflowId == workflowId && x.ClientEventId == clientEventId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Status, update.Status)
                    .SetProperty(x => x.UpdatedAt, update.UtcNow)
                    .SetProperty(x => x.AppliedAt, update.AppliedAt)
                    .SetProperty(x => x.ErrorCode, update.ErrorCode),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }
}
