using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>PostgreSQL の行ロックを使用する永続実行ワークキュー。</summary>
internal sealed class ExecutionWorkQueue(IDbContextFactory<CoreDbContext> dbFactory) : IExecutionWorkQueue
{
    /// <inheritdoc />
    public Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        return EnqueueManyAsync([item], ct);
    }

    /// <inheritdoc />
    public async Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ExecutionWorkItems.AddRange(items);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
        string leaseOwner,
        DateTime utcNow,
        TimeSpan leaseDuration,
        int limit,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var leaseUntil = utcNow.Add(leaseDuration);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
            .ConfigureAwait(false);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            WITH candidates AS (
                SELECT work_item_id
                FROM execution_work_items
                WHERE available_at <= @utcNow
                  AND (lease_until IS NULL OR lease_until <= @utcNow)
                ORDER BY available_at, created_at, work_item_id
                FOR UPDATE SKIP LOCKED
                LIMIT @limit
            )
            UPDATE execution_work_items AS item
            SET lease_owner = @leaseOwner,
                lease_until = @leaseUntil,
                attempts = item.attempts + 1
            FROM candidates
            WHERE item.work_item_id = candidates.work_item_id
            RETURNING item.*;
            """;
        AddParameter(command, "utcNow", utcNow);
        AddParameter(command, "limit", limit);
        AddParameter(command, "leaseOwner", leaseOwner);
        AddParameter(command, "leaseUntil", leaseUntil);

        var items = new List<ExecutionWorkItemRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var leaseOwnerOrdinal = reader.GetOrdinal("lease_owner");
        var leaseUntilOrdinal = reader.GetOrdinal("lease_until");
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new ExecutionWorkItemRow
            {
                WorkItemId = reader.GetGuid(reader.GetOrdinal("work_item_id")),
                ExecutionId = reader.GetGuid(reader.GetOrdinal("execution_id")),
                Kind = reader.GetString(reader.GetOrdinal("kind")),
                Payload = reader.GetString(reader.GetOrdinal("payload")),
                AvailableAt = reader.GetDateTime(reader.GetOrdinal("available_at")),
                LeaseOwner = await reader.IsDBNullAsync(leaseOwnerOrdinal, ct).ConfigureAwait(false)
                    ? null
                    : reader.GetString(leaseOwnerOrdinal),
                LeaseUntil = await reader.IsDBNullAsync(leaseUntilOrdinal, ct).ConfigureAwait(false)
                    ? null
                    : reader.GetDateTime(leaseUntilOrdinal),
                Attempts = reader.GetInt32(reader.GetOrdinal("attempts")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            });
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return items;
    }

    /// <inheritdoc />
    public async Task<bool> RenewLeaseAsync(
        Guid workItemId,
        string leaseOwner,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var leaseUntil = utcNow.Add(leaseDuration);
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var updated = await db.ExecutionWorkItems
            .Where(item => item.WorkItemId == workItemId && item.LeaseOwner == leaseOwner)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.LeaseUntil, leaseUntil),
                ct)
            .ConfigureAwait(false);
        return updated > 0;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.ExecutionWorkItems
            .Where(item => item.WorkItemId == workItemId && item.LeaseOwner == leaseOwner)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.ExecutionWorkItems
            .Where(item => item.WorkItemId == workItemId && item.LeaseOwner == leaseOwner)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.LeaseOwner, (string?)null)
                    .SetProperty(item => item.LeaseUntil, (DateTime?)null)
                    .SetProperty(item => item.AvailableAt, availableAt),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>DB コマンドへ値を安全にパラメーター追加する。</summary>
    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
