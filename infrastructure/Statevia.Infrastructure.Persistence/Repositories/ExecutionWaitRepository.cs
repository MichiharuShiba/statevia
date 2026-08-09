using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>execution_waits / execution_wait_subscriptions 永続化。</summary>
internal sealed class ExecutionWaitRepository(
    IDbContextFactory<CoreDbContext> dbFactory,
    IIdGenerator idGenerator) : IExecutionWaitRepository
{
    /// <inheritdoc />
    public async Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var existingRows = await db.ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var desiredNodeIds = waits.Select(x => x.NodeId).ToHashSet(StringComparer.Ordinal);
        var staleRows = existingRows.Where(x => !desiredNodeIds.Contains(x.NodeId)).ToList();
        if (staleRows.Count > 0)
            db.ExecutionWaits.RemoveRange(staleRows);

        var existingByNodeId = existingRows.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
        foreach (var wait in waits)
        {
            if (existingByNodeId.TryGetValue(wait.NodeId, out var existing))
            {
                existing.WaitKind = wait.WaitKind;
                existing.AllowedEvents = wait.AllowedEvents;
                existing.ExpiresAt = wait.ExpiresAt;
                existing.CreatedAt = wait.CreatedAt;
                continue;
            }

            db.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = wait.NodeId,
                WaitKind = wait.WaitKind,
                AllowedEvents = wait.AllowedEvents,
                ExpiresAt = wait.ExpiresAt,
                CreatedAt = wait.CreatedAt
            });
        }

        var existingSubscriptions = await db.ExecutionWaitSubscriptions
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        db.ExecutionWaitSubscriptions.RemoveRange(existingSubscriptions);

        var subscriptionRows = waits
            .SelectMany(wait => wait.Subscriptions.Select(subscription => new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = subscription.SubscriptionId == Guid.Empty
                    ? idGenerator.NewSequentialGuid()
                    : subscription.SubscriptionId,
                ExecutionId = executionId,
                NodeId = wait.NodeId,
                Topic = subscription.Topic,
                CorrelationKey = subscription.CorrelationKey,
                ResumeEventName = subscription.ResumeEventName,
                CreatedAt = subscription.CreatedAt == default ? wait.CreatedAt : subscription.CreatedAt
            }))
            .ToList();
        if (subscriptionRows.Count > 0)
            db.ExecutionWaitSubscriptions.AddRange(subscriptionRows);
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

        // 子購読は FK CASCADE で削除される。
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
    public async Task<IReadOnlyList<MatchingWaitSubscription>> ListMatchingSubscriptionsAsync(
        ICoreUnitOfWork uow,
        string topic,
        string correlationKey,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(correlationKey);

        return await uow.GetDb().ExecutionWaitSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.Topic == topic
                && subscription.CorrelationKey == correlationKey)
            .OrderBy(subscription => subscription.CreatedAt)
            .ThenBy(subscription => subscription.ExecutionId)
            .ThenBy(subscription => subscription.NodeId)
            .Select(subscription => new MatchingWaitSubscription(
                subscription.ExecutionId,
                subscription.NodeId,
                subscription.ResumeEventName))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
