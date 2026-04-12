using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

/// <summary>
/// <see cref="IEventDeliveryDedupRepository"/> の EF Core 実装。
/// </summary>
public sealed class EventDeliveryDedupRepository : IEventDeliveryDedupRepository
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
        string status,
        DateTime utcNow,
        DateTime? appliedAt,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        var affected = await db.EventDeliveryDedup
            .Where(x => x.TenantId == tenantId && x.WorkflowId == workflowId && x.ClientEventId == clientEventId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.AppliedAt, appliedAt)
                    .SetProperty(x => x.ErrorCode, errorCode),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }
}
