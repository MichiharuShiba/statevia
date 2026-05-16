using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

/// <summary>
/// <see cref="IEventDeliveryDedupRepository"/> の EF Core 実装。
/// </summary>
internal sealed class EventDeliveryDedupRepository : IEventDeliveryDedupRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    /// <summary>
    /// 新しいインスタンスを初期化する。
    /// </summary>
    public EventDeliveryDedupRepository(IDbContextFactory<CoreDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task<EventDeliveryDedupRow?> FindAsync(
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.EventDeliveryDedup.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.WorkflowId == workflowId && x.ClientEventId == clientEventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertReceivedAsync(EventDeliveryDedupRow row, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.EventDeliveryDedup.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task AddReceivedAsync(CoreDbContext db, EventDeliveryDedupRow row, CancellationToken cancellationToken)
    {
        db.EventDeliveryDedup.Add(row);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateStatusAsync(
        CoreDbContext db,
        string tenantId,
        Guid workflowId,
        Guid clientEventId,
        EventDeliveryDedupStatusUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var affected = await db.EventDeliveryDedup
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
