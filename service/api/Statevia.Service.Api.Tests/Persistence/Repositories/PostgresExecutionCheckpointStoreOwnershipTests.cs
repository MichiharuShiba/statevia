using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="PostgresExecutionCheckpointStore"/> の所有・fencing シナリオ。</summary>
public sealed class PostgresExecutionCheckpointStoreOwnershipTests
{
    /// <summary>旧世代の runtime upsert は拒否され、新世代のみ成功する。</summary>
    [Fact]
    public async Task TryUpsertRuntimeWithGenerationAsync_RejectsStaleGeneration()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var store = new PostgresExecutionCheckpointStore();
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        long generation;
        await using (var uow = await uowFactory.CreateAsync())
        {
            var acquired = await store.TryAcquireOwnershipAsync(
                uow,
                executionId,
                workerId: "worker-a",
                leaseUntilUtc: DateTime.UtcNow.AddMinutes(1),
                seed: new ExecutionCheckpointDocument
                {
                    ExecutionId = executionId,
                    CheckpointJson = """{"schemaVersion":1}""",
                    SchemaVersion = 1,
                    UpdatedAt = DateTime.UtcNow
                },
                CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
            Assert.True(acquired.HasValue);
            generation = acquired.Value;
        }

        // Act
        bool staleAccepted;
        bool freshAccepted;
        await using (var uow = await uowFactory.CreateAsync())
        {
            staleAccepted = await store.TryUpsertRuntimeWithGenerationAsync(
                uow,
                new ExecutionCheckpointRuntimeUpsert(
                    executionId,
                    OwnerGeneration: generation - 1,
                    CheckpointJson: """{"stale":true}""",
                    SchemaVersion: 1,
                    UpdatedAtUtc: DateTime.UtcNow),
                CancellationToken.None);
            freshAccepted = await store.TryUpsertRuntimeWithGenerationAsync(
                uow,
                new ExecutionCheckpointRuntimeUpsert(
                    executionId,
                    OwnerGeneration: generation,
                    CheckpointJson: """{"fresh":true}""",
                    SchemaVersion: 1,
                    UpdatedAtUtc: DateTime.UtcNow),
                CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        Assert.False(staleAccepted);
        Assert.True(freshAccepted);
        await using var verify = new CoreDbContext(db.Options);
        var row = await verify.ExecutionCheckpoints.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("""{"fresh":true}""", row.CheckpointJson);
        Assert.Equal(generation, row.OwnerGeneration);
    }

    /// <summary>期限切れ所有の世代バンプ後、旧世代の lease 延長は失敗する。</summary>
    [Fact]
    public async Task TryBumpGenerationAndClearExpiredOwner_RejectsOldHeartbeat()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var store = new PostgresExecutionCheckpointStore();
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        long oldGeneration;
        await using (var uow = await uowFactory.CreateAsync())
        {
            var acquired = await store.TryAcquireOwnershipAsync(
                uow,
                executionId,
                workerId: "worker-a",
                leaseUntilUtc: DateTime.UtcNow.AddMinutes(-1),
                seed: new ExecutionCheckpointDocument
                {
                    ExecutionId = executionId,
                    CheckpointJson = "{}",
                    SchemaVersion = 1,
                    UpdatedAt = DateTime.UtcNow
                },
                CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
            Assert.True(acquired.HasValue);
            oldGeneration = acquired.Value;
        }

        // Act
        await using (var uow = await uowFactory.CreateAsync())
        {
            var bumped = await store.TryBumpGenerationAndClearExpiredOwnerAsync(
                uow,
                executionId,
                DateTime.UtcNow,
                CancellationToken.None);
            Assert.True(bumped);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        bool renewed;
        await using (var uow = await uowFactory.CreateAsync())
        {
            renewed = await store.TryRenewOwnershipLeaseAsync(
                uow,
                executionId,
                oldGeneration,
                DateTime.UtcNow.AddMinutes(1),
                CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        Assert.False(renewed);
        await using var verify = new CoreDbContext(db.Options);
        var row = await verify.ExecutionCheckpoints.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Null(row.OwnerWorkerId);
        Assert.Equal(oldGeneration + 1, row.OwnerGeneration);
    }

    private static async Task SeedExecutionAsync(InMemoryTestDatabase db, Guid executionId)
    {
        var now = DateTime.UtcNow;
        await using var ctx = new CoreDbContext(db.Options);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });
        await ctx.SaveChangesAsync();
    }
}
