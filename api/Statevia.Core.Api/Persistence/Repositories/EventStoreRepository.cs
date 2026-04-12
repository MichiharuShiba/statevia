using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    /// <inheritdoc />
    public async Task<bool> TryAppendIfAbsentByClientEventAsync(
        CoreDbContext db,
        Guid workflowId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var typeString = eventType.ToPersistedString();
        var fingerprintEventId = ComputeFingerprintEventId(workflowId, clientEventId, typeString);

        if (db.EventStore.Local.Any(e => e.EventId == fingerprintEventId))
            return false;

        if (await db.EventStore.AsNoTracking()
                .AnyAsync(e => e.EventId == fingerprintEventId, cancellationToken)
                .ConfigureAwait(false))
            return false;

        var persistedMax = await db.EventStore
            .AsNoTracking()
            .Where(e => e.WorkflowId == workflowId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0L;

        var localMax = db.ChangeTracker.Entries<EventStoreRow>()
            .Where(e => e.Entity.WorkflowId == workflowId && e.State != EntityState.Deleted)
            .Select(e => e.Entity.Seq)
            .DefaultIfEmpty(0L)
            .Max();

        var nextSeq = Math.Max(persistedMax, localMax) + 1L;
        var now = DateTime.UtcNow;

        db.EventStore.Add(new EventStoreRow
        {
            EventId = fingerprintEventId,
            WorkflowId = workflowId,
            Seq = nextSeq,
            Type = typeString,
            OccurredAt = now,
            SchemaVersion = 1,
            PayloadJson = payloadJson,
            CreatedAt = now
        });

        return true;
    }

    /// <summary>
    /// 同一配送を表す安定した <see cref="EventStoreRow.EventId"/>（DB の event_id UNIQUE による insert-skip）。
    /// </summary>
    private static Guid ComputeFingerprintEventId(Guid workflowId, Guid clientEventId, string typeString)
    {
        var input = $"{workflowId:N}|{clientEventId:N}|{typeString}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
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
