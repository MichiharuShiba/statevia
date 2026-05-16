using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

/// <summary><see cref="EventDeliveryDedupRepository"/> の SQLite 統合テスト。</summary>
public sealed class EventDeliveryDedupRepositoryTests
{
    /// <summary>キーが存在しないとき null を返す。</summary>
    [Fact]
    public async Task FindAsync_ReturnsNull_WhenRowMissing()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.NewGuid();

        // Act
        var row = await repo.FindAsync("tenant-a", workflowId, clientEventId, default);

        // Assert
        Assert.Null(row);
    }

    /// <summary>InsertReceivedAsync で RECEIVED 行が永続化される。</summary>
    [Fact]
    public async Task InsertReceivedAsync_PersistsReceivedRow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.NewGuid();
        var acceptedAt = new DateTime(2026, 5, 16, 12, 0, 0, DateTimeKind.Utc);
        var row = new EventDeliveryDedupRow
        {
            TenantId = "tenant-a",
            WorkflowId = workflowId,
            ClientEventId = clientEventId,
            BatchId = Guid.NewGuid(),
            Status = EventDeliveryDedupStatuses.Received,
            AcceptedAt = acceptedAt,
            UpdatedAt = acceptedAt
        };

        // Act
        await repo.InsertReceivedAsync(row, default);
        var found = await repo.FindAsync("tenant-a", workflowId, clientEventId, default);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(EventDeliveryDedupStatuses.Received, found.Status);
        Assert.Equal(acceptedAt, found.AcceptedAt);
        Assert.Equal(row.BatchId, found.BatchId);
    }

    /// <summary>同一キーの二重 INSERT は DbUpdateException となり一意制約違反と判定できる。</summary>
    [Fact]
    public async Task InsertReceivedAsync_ThrowsDbUpdateException_WhenDuplicateKey()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.NewGuid();
        var acceptedAt = DateTime.UtcNow;
        var row = new EventDeliveryDedupRow
        {
            TenantId = "tenant-a",
            WorkflowId = workflowId,
            ClientEventId = clientEventId,
            Status = EventDeliveryDedupStatuses.Received,
            AcceptedAt = acceptedAt,
            UpdatedAt = acceptedAt
        };

        await repo.InsertReceivedAsync(row, default);

        // Act
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            repo.InsertReceivedAsync(row, default));

        // Assert
        Assert.True(EventDeliveryRetryPolicy.IsUniqueConstraintViolation(ex));
    }

    /// <summary>TryUpdateStatusAsync は ExecuteUpdate で行を更新し true を返す。</summary>
    [Fact]
    public async Task TryUpdateStatusAsync_UpdatesStatus_WhenRowExists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.NewGuid();
        var acceptedAt = new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Utc);
        var appliedAt = new DateTime(2026, 5, 16, 10, 1, 0, DateTimeKind.Utc);

        await using (var seed = new CoreDbContext(db.Options))
        {
            seed.EventDeliveryDedup.Add(new EventDeliveryDedupRow
            {
                TenantId = "tenant-a",
                WorkflowId = workflowId,
                ClientEventId = clientEventId,
                Status = EventDeliveryDedupStatuses.Received,
                AcceptedAt = acceptedAt,
                UpdatedAt = acceptedAt
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = new CoreDbContext(db.Options);

        // Act
        var updated = await repo.TryUpdateStatusAsync(
            ctx,
            "tenant-a",
            workflowId,
            clientEventId,
            new EventDeliveryDedupStatusUpdate(
                EventDeliveryDedupStatuses.Applied,
                appliedAt,
                AppliedAt: appliedAt,
                ErrorCode: null),
            default);

        // Assert
        Assert.True(updated);
        var found = await repo.FindAsync("tenant-a", workflowId, clientEventId, default);
        Assert.NotNull(found);
        Assert.Equal(EventDeliveryDedupStatuses.Applied, found.Status);
        Assert.Equal(appliedAt, found.AppliedAt);
        Assert.Equal(appliedAt, found.UpdatedAt);
    }

    /// <summary>対象行が無いとき TryUpdateStatusAsync は false を返す。</summary>
    [Fact]
    public async Task TryUpdateStatusAsync_ReturnsFalse_WhenRowMissing()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        await using var ctx = new CoreDbContext(db.Options);

        // Act
        var updated = await repo.TryUpdateStatusAsync(
            ctx,
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new EventDeliveryDedupStatusUpdate(
                EventDeliveryDedupStatuses.Failed,
                DateTime.UtcNow,
                AppliedAt: null,
                ErrorCode: "ERR"),
            default);

        // Assert
        Assert.False(updated);
    }

    /// <summary>AddReceivedAsync は SaveChanges 前は DB に反映されない。</summary>
    [Fact]
    public async Task AddReceivedAsync_DoesNotPersistUntilSaveChanges()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new EventDeliveryDedupRepository(db.Factory);
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.NewGuid();
        var acceptedAt = DateTime.UtcNow;
        var row = new EventDeliveryDedupRow
        {
            TenantId = "tenant-a",
            WorkflowId = workflowId,
            ClientEventId = clientEventId,
            Status = EventDeliveryDedupStatuses.Received,
            AcceptedAt = acceptedAt,
            UpdatedAt = acceptedAt
        };

        await using var ctx = new CoreDbContext(db.Options);

        // Act
        await repo.AddReceivedAsync(ctx, row, default);
        var beforeSave = await repo.FindAsync("tenant-a", workflowId, clientEventId, default);
        await ctx.SaveChangesAsync();
        var afterSave = await repo.FindAsync("tenant-a", workflowId, clientEventId, default);

        // Assert
        Assert.Null(beforeSave);
        Assert.NotNull(afterSave);
    }
}
