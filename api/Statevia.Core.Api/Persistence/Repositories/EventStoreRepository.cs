using System;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Persistence.Repositories;

public sealed class EventStoreRepository : IEventStoreRepository
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly IIdGenerator _ids;

    public EventStoreRepository(IDbContextFactory<CoreDbContext> dbFactory, IIdGenerator ids)
    {
        _dbFactory = dbFactory;
        _ids = ids;
    }

    public async Task AppendAsync(Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var maxSeq = await db.EventStore
            .Where(e => e.WorkflowId == workflowId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        var nextSeq = (maxSeq ?? 0L) + 1L;
        var now = DateTime.UtcNow;

        db.EventStore.Add(new EventStoreRow
        {
            EventId = _ids.NewGuid(),
            WorkflowId = workflowId,
            Seq = nextSeq,
            Type = eventType.ToPersistedString(),
            OccurredAt = now,
            SchemaVersion = 1,
            PayloadJson = payloadJson,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task AppendAsync(CoreDbContext db, Guid workflowId, EventStoreEventType eventType, string? payloadJson, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var maxSeq = await db.EventStore
            .Where(e => e.WorkflowId == workflowId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        var nextSeq = (maxSeq ?? 0L) + 1L;
        var now = DateTime.UtcNow;

        db.EventStore.Add(new EventStoreRow
        {
            EventId = _ids.NewGuid(),
            WorkflowId = workflowId,
            Seq = nextSeq,
            Type = eventType.ToPersistedString(),
            OccurredAt = now,
            SchemaVersion = 1,
            PayloadJson = payloadJson,
            CreatedAt = now
        });
    }

    public async Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        Guid workflowId,
        long afterSeq,
        int limit,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSeq);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var take = limit + 1;
        var list = await db.EventStore.AsNoTracking()
            .Where(e => e.WorkflowId == workflowId && e.Seq > afterSeq)
            .OrderBy(e => e.Seq)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var hasMore = list.Count > limit;
        if (hasMore)
            list.RemoveAt(list.Count - 1);

        return (list, hasMore);
    }

    public async Task<long> GetMaxSeqAsync(Guid workflowId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var max = await db.EventStore.AsNoTracking()
            .Where(e => e.WorkflowId == workflowId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        return max ?? 0L;
    }
}
