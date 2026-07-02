using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Persistence.Repositories;

internal sealed class EventStoreRepository : IEventStoreRepository
{
    private readonly IIdGenerator _ids;

    public EventStoreRepository(IIdGenerator ids) => _ids = ids;

    public async Task AppendAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken ct = default)
    {
        var persistedMax = await uow.GetDb().EventStore
            .AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(ct)
            .ConfigureAwait(false) ?? 0L;

        var localMax = uow.GetDb().ChangeTracker.Entries<EventStoreRow>()
            .Where(e => e.Entity.ExecutionId == executionId && e.State != EntityState.Deleted)
            .Select(e => e.Entity.Seq)
            .DefaultIfEmpty(0L)
            .Max();

        var nextSeq = Math.Max(persistedMax, localMax) + 1L;
        var now = DateTime.UtcNow;

        uow.GetDb().EventStore.Add(new EventStoreRow
        {
            EventId = _ids.NewGuid(),
            ExecutionId = executionId,
            Seq = nextSeq,
            Type = eventType.ToPersistedString(),
            OccurredAt = now,
            SchemaVersion = 1,
            PayloadJson = payloadJson,
            CreatedAt = now
        });
    }

    public async Task<bool> TryAppendIfAbsentByClientEventAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        Guid clientEventId,
        EventStoreEventType eventType,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        var typeString = eventType.ToPersistedString();
        var fingerprintEventId = ComputeFingerprintEventId(executionId, clientEventId, typeString);

        if (uow.GetDb().EventStore.Local.Any(e => e.EventId == fingerprintEventId))
            return false;

        if (await uow.GetDb().EventStore.AsNoTracking()
                .AnyAsync(e => e.EventId == fingerprintEventId, cancellationToken)
                .ConfigureAwait(false))
            return false;

        var persistedMax = await uow.GetDb().EventStore
            .AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0L;

        var localMax = uow.GetDb().ChangeTracker.Entries<EventStoreRow>()
            .Where(e => e.Entity.ExecutionId == executionId && e.State != EntityState.Deleted)
            .Select(e => e.Entity.Seq)
            .DefaultIfEmpty(0L)
            .Max();

        var nextSeq = Math.Max(persistedMax, localMax) + 1L;
        var now = DateTime.UtcNow;

        uow.GetDb().EventStore.Add(new EventStoreRow
        {
            EventId = fingerprintEventId,
            ExecutionId = executionId,
            Seq = nextSeq,
            Type = typeString,
            OccurredAt = now,
            SchemaVersion = 1,
            PayloadJson = payloadJson,
            CreatedAt = now
        });

        return true;
    }

    public async Task<(IReadOnlyList<EventStoreRow> Items, bool HasMore)> ListAfterSeqAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long afterSeq,
        int limit,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSeq);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var take = limit + 1;
        var list = await uow.GetDb().EventStore.AsNoTracking()
            .Where(e => e.ExecutionId == executionId && e.Seq > afterSeq)
            .OrderBy(e => e.Seq)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var hasMore = list.Count > limit;
        if (hasMore)
            list.RemoveAt(list.Count - 1);

        return (list, hasMore);
    }

    public async Task<long> GetMaxSeqAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct = default)
    {
        var max = await uow.GetDb().EventStore.AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .Select(e => (long?)e.Seq)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        return max ?? 0L;
    }

    private static Guid ComputeFingerprintEventId(Guid executionId, Guid clientEventId, string typeString)
    {
        var input = $"{executionId:N}|{clientEventId:N}|{typeString}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
