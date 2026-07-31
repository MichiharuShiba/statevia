using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionWaitRepository"/> の永続化シナリオ。</summary>
public sealed class ExecutionWaitRepositoryTests
{
    /// <summary>ReplaceWaitsAsync は不在 wait を削除し、指定 wait を upsert する。</summary>
    [Fact]
    public async Task ReplaceWaitsAsync_ReplacesRowsForExecution()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
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
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "stale",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["old"],
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var desired = new[]
        {
            new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "wait-node",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["approve", "reject"],
                CreatedAt = now
            }
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.ReplaceWaitsAsync(uow, executionId, desired, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var rows = await verify.ExecutionWaits.Where(x => x.ExecutionId == executionId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("wait-node", rows[0].NodeId);
        Assert.Equal(["approve", "reject"], rows[0].AllowedEvents);
    }

    /// <summary>DeleteByNodeIdAsync は node_id 一致行のみ削除する。</summary>
    [Fact]
    public async Task DeleteByNodeIdAsync_RemovesMatchingRows()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory);
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
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
            ctx.ExecutionWaits.AddRange(
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n1",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["approve"],
                    CreatedAt = now
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n2",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["other"],
                    CreatedAt = now
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.DeleteByNodeIdAsync(uow, executionId, "n1", CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var nodeIds = await verify.ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .Select(x => x.NodeId)
            .ToListAsync();
        Assert.Single(nodeIds);
        Assert.Equal("n2", nodeIds[0]);
    }

    /// <summary>ListByExecutionIdAsync は created_at 順で wait 行を返す。</summary>
    [Fact]
    public async Task ListByExecutionIdAsync_ReturnsRowsOrdered()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory);
        var executionId = Guid.NewGuid();
        var t1 = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 5, 26, 0, 0, 1, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = t1,
                UpdatedAt = t1,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionWaits.AddRange(
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n2",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["second"],
                    CreatedAt = t2
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "n1",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["first"],
                    CreatedAt = t1
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var rows = await repo.ListByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(["first"], rows[0].AllowedEvents);
        Assert.Equal(["second"], rows[1].AllowedEvents);
    }
}
