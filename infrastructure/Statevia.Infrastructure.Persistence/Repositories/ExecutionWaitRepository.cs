using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>execution_waits 永続化。</summary>
internal sealed class ExecutionWaitRepository(IDbContextFactory<CoreDbContext> dbFactory) : IExecutionWaitRepository
{
    /// <inheritdoc />
    public async Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct)
    {
        var existingRows = await uow.GetDb().ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var desiredNodeIds = waits.Select(x => x.NodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in existingRows.Where(x => !desiredNodeIds.Contains(x.NodeId)))
            uow.GetDb().ExecutionWaits.Remove(stale);

        var existingByNodeId = existingRows.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
        foreach (var wait in waits)
        {
            if (existingByNodeId.TryGetValue(wait.NodeId, out var existing))
            {
                existing.WaitKind = wait.WaitKind;
                existing.AllowedEvents = wait.AllowedEvents;
                existing.ExpiresAt = wait.ExpiresAt;
                existing.CorrelationKey = wait.CorrelationKey;
                existing.Topic = wait.Topic;
                existing.CreatedAt = wait.CreatedAt;
                continue;
            }

            uow.GetDb().ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = wait.NodeId,
                WaitKind = wait.WaitKind,
                AllowedEvents = wait.AllowedEvents,
                ExpiresAt = wait.ExpiresAt,
                CorrelationKey = wait.CorrelationKey,
                Topic = wait.Topic,
                CreatedAt = wait.CreatedAt
            });
        }
    }

    /// <inheritdoc />
    public async Task DeleteByNodeIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var rows = await uow.GetDb().ExecutionWaits
            .Where(x => x.ExecutionId == executionId && x.NodeId == nodeId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (rows.Count == 0)
            return;

        uow.GetDb().ExecutionWaits.RemoveRange(rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionWaitRow>> ListExpiredDelayWaitsAsync(
        DateTime utcNow,
        int limit,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ExecutionWaits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(wait =>
                wait.WaitKind == ExecutionWaitKind.DelayWait
                && wait.ExpiresAt != null
                && wait.ExpiresAt <= utcNow)
            .OrderBy(wait => wait.ExpiresAt)
            .ThenBy(wait => wait.ExecutionId)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct) =>
        await uow.GetDb().ExecutionWaits.AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.NodeId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionWaitRow>> ListMatchingEventWaitsAsync(
        ICoreUnitOfWork uow,
        string eventName,
        string? correlationKey,
        string? topic,
        CancellationToken ct)
    {
        var candidates = await uow.GetDb().ExecutionWaits
            .Where(wait =>
                wait.WaitKind == ExecutionWaitKind.EventWait
                && wait.CorrelationKey == correlationKey
                && wait.Topic == topic)
            .OrderBy(wait => wait.CreatedAt)
            .ThenBy(wait => wait.ExecutionId)
            .ThenBy(wait => wait.NodeId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return candidates
            .Where(wait => wait.AllowedEvents.Any(allowed =>
                string.Equals(allowed, eventName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
