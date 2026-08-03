using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL 上の実行チェックポイント文書ストア実装。
/// </summary>
/// <remarks>
/// 物理テーブルは <c>execution_runtime_checkpoints</c>。
/// 所有更新は <c>owner_generation</c> 一致条件付きで fencing する。
/// </remarks>
internal sealed class PostgresExecutionCheckpointStore : IExecutionCheckpointStore
{
    /// <inheritdoc />
    public async Task UpsertAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointDocument document,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(x => x.ExecutionId == document.ExecutionId, ct)
            .ConfigureAwait(false);
        if (existing is null)
        {
            db.ExecutionCheckpoints.Add(document);
            return;
        }

        // runtime JSON のみ更新。所有メタは専用 API（acquire / renew / clear）で更新する。
        existing.CheckpointJson = document.CheckpointJson;
        existing.SchemaVersion = document.SchemaVersion;
        existing.UpdatedAt = document.UpdatedAt;
    }

    /// <inheritdoc />
    public Task<ExecutionCheckpointDocument?> GetByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct) =>
        uow.GetDb().ExecutionCheckpoints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct);

    /// <inheritdoc />
    public async Task DeleteAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct)
    {
        var documents = await uow.GetDb().ExecutionCheckpoints
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (documents.Count == 0)
            return;

        uow.GetDb().ExecutionCheckpoints.RemoveRange(documents);
    }

    /// <inheritdoc />
    public async Task<long?> TryAcquireOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string workerId,
        DateTime leaseUntilUtc,
        ExecutionCheckpointDocument? seed,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(x => x.ExecutionId == executionId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            if (seed is null)
                return null;

            seed.ExecutionId = executionId;
            seed.OwnerWorkerId = workerId;
            seed.LeaseUntil = leaseUntilUtc;
            seed.OwnerGeneration = 1;
            db.ExecutionCheckpoints.Add(seed);
            return 1;
        }

        // 未所有、または自 Worker の再獲得、または期限切れのみ奪取可（通常 Start は未所有想定）。
        if (existing.OwnerWorkerId is not null
            && !string.Equals(existing.OwnerWorkerId, workerId, StringComparison.Ordinal)
            && existing.LeaseUntil is { } until
            && until > DateTime.UtcNow)
        {
            return null;
        }

        existing.OwnerWorkerId = workerId;
        existing.LeaseUntil = leaseUntilUtc;
        existing.OwnerGeneration += 1;
        existing.UpdatedAt = DateTime.UtcNow;
        return existing.OwnerGeneration;
    }

    /// <inheritdoc />
    public async Task<long?> TrySeizeExpiredOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string workerId,
        DateTime nowUtc,
        DateTime newLeaseUntilUtc,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(
                x => x.ExecutionId == executionId
                    && x.OwnerWorkerId != null
                    && x.LeaseUntil != null
                    && x.LeaseUntil < nowUtc,
                ct)
            .ConfigureAwait(false);
        if (existing is null)
            return null;

        existing.OwnerWorkerId = workerId;
        existing.LeaseUntil = newLeaseUntilUtc;
        existing.OwnerGeneration += 1;
        existing.UpdatedAt = nowUtc;
        return existing.OwnerGeneration;
    }

    /// <inheritdoc />
    public async Task<bool> TryRenewOwnershipLeaseAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long ownerGeneration,
        DateTime leaseUntilUtc,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(
                x => x.ExecutionId == executionId && x.OwnerGeneration == ownerGeneration,
                ct)
            .ConfigureAwait(false);
        if (existing is null)
            return false;

        existing.LeaseUntil = leaseUntilUtc;
        existing.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> TryUpsertRuntimeWithGenerationAsync(
        ICoreUnitOfWork uow,
        ExecutionCheckpointRuntimeUpsert upsert,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upsert);
        ArgumentException.ThrowIfNullOrWhiteSpace(upsert.CheckpointJson);
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(
                x => x.ExecutionId == upsert.ExecutionId && x.OwnerGeneration == upsert.OwnerGeneration,
                ct)
            .ConfigureAwait(false);
        if (existing is null)
            return false;

        existing.CheckpointJson = upsert.CheckpointJson;
        existing.SchemaVersion = upsert.SchemaVersion;
        existing.UpdatedAt = upsert.UpdatedAtUtc;
        if (upsert.LeaseUntilUtc is { } lease)
            existing.LeaseUntil = lease;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> TryClearOwnershipAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        long ownerGeneration,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(
                x => x.ExecutionId == executionId && x.OwnerGeneration == ownerGeneration,
                ct)
            .ConfigureAwait(false);
        if (existing is null)
            return false;

        existing.OwnerWorkerId = null;
        existing.LeaseUntil = null;
        existing.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListExpiredOwnedExecutionIdsAsync(
        ICoreUnitOfWork uow,
        DateTime nowUtc,
        int limit,
        CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        return await uow.GetDb().ExecutionCheckpoints.AsNoTracking()
            .Where(x => x.OwnerWorkerId != null && x.LeaseUntil != null && x.LeaseUntil < nowUtc)
            .OrderBy(x => x.LeaseUntil)
            .Select(x => x.ExecutionId)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TryBumpGenerationAndClearExpiredOwnerAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var db = uow.GetDb();
        var existing = await db.ExecutionCheckpoints
            .FirstOrDefaultAsync(
                x => x.ExecutionId == executionId
                    && x.OwnerWorkerId != null
                    && x.LeaseUntil != null
                    && x.LeaseUntil < nowUtc,
                ct)
            .ConfigureAwait(false);
        if (existing is null)
            return false;

        existing.OwnerWorkerId = null;
        existing.LeaseUntil = null;
        existing.OwnerGeneration += 1;
        existing.UpdatedAt = nowUtc;
        return true;
    }
}
