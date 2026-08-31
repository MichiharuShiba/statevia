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
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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

    /// <summary>ReplaceWaitsAsync は親 wait と同時に子購読を全置換する。</summary>
    [Fact]
    public async Task ReplaceWaitsAsync_ReplacesSubscriptionsForExecution()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(CreateExecution(executionId, now));
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "old-wait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["statevia.event.subscribe.0"],
                CreatedAt = now
            });
            ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = Guid.NewGuid(),
                ExecutionId = executionId,
                NodeId = "old-wait",
                Topic = "stale.topic",
                CorrelationKey = "",
                ResumeEventName = "statevia.event.subscribe.0",
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
                AllowedEvents = ["statevia.event.subscribe.0", "statevia.event.subscribe.1"],
                CreatedAt = now,
                Subscriptions =
                [
                    new ExecutionWaitSubscriptionRow
                    {
                        SubscriptionId = Guid.NewGuid(),
                        ExecutionId = executionId,
                        NodeId = "wait-node",
                        Topic = "inventory.received",
                        CorrelationKey = "SKU-1",
                        ResumeEventName = "statevia.event.subscribe.0",
                        CreatedAt = now
                    },
                    new ExecutionWaitSubscriptionRow
                    {
                        SubscriptionId = Guid.NewGuid(),
                        ExecutionId = executionId,
                        NodeId = "wait-node",
                        Topic = "inventory.returned",
                        CorrelationKey = "",
                        ResumeEventName = "statevia.event.subscribe.1",
                        CreatedAt = now
                    }
                ]
            }
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.ReplaceWaitsAsync(uow, executionId, desired, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var waits = await verify.ExecutionWaits.Where(x => x.ExecutionId == executionId).ToListAsync();
        var subscriptions = await verify.ExecutionWaitSubscriptions
            .Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.ResumeEventName)
            .ToListAsync();
        Assert.Single(waits);
        Assert.Equal("wait-node", waits[0].NodeId);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal("inventory.received", subscriptions[0].Topic);
        Assert.Equal("SKU-1", subscriptions[0].CorrelationKey);
        Assert.Equal("inventory.returned", subscriptions[1].Topic);
        Assert.Equal("", subscriptions[1].CorrelationKey);
    }

    /// <summary>topic+空 key は厳密一致のみ返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_MatchesEmptyKeyExactly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db,
            executionId,
            "wait-a",
            "orders.updated",
            "",
            "statevia.event.subscribe.0",
            now);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var emptyKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "", CancellationToken.None);
        var keyedMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-1", CancellationToken.None);

        // Assert
        Assert.Single(emptyKeyMatches);
        Assert.Equal(executionId, emptyKeyMatches[0].ExecutionId);
        Assert.Equal("statevia.event.subscribe.0", emptyKeyMatches[0].ResumeEventName);
        Assert.Empty(keyedMatches);
    }

    /// <summary>topic+非空 key は厳密一致のみ返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_MatchesNonEmptyKeyExactly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db,
            executionId,
            "wait-a",
            "orders.updated",
            "order-1",
            "statevia.event.subscribe.0",
            now);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var keyedMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-1", CancellationToken.None);
        var emptyKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "", CancellationToken.None);
        var otherKeyMatches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "orders.updated", "order-2", CancellationToken.None);

        // Assert
        Assert.Single(keyedMatches);
        Assert.Equal("wait-a", keyedMatches[0].NodeId);
        Assert.Empty(emptyKeyMatches);
        Assert.Empty(otherKeyMatches);
    }

    /// <summary>同一 topic+key の複数購読をすべて返す。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_ReturnsMultipleMatches()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var execution1 = Guid.NewGuid();
        var execution2 = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db, execution1, "wait-a", "inventory.received", "SKU-1", "statevia.event.subscribe.0", now);
        await SeedSubscriptionAsync(
            db, execution2, "wait-b", "inventory.received", "SKU-1", "statevia.event.subscribe.0", now.AddSeconds(1));

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "inventory.received", "SKU-1", CancellationToken.None);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.ExecutionId == execution1 && m.NodeId == "wait-a");
        Assert.Contains(matches, m => m.ExecutionId == execution2 && m.NodeId == "wait-b");
    }

    /// <summary>DisabledTenantQueryFilterOptions でも他テナントの同一 topic/key は返さない。</summary>
    [Fact]
    public async Task ListMatchingSubscriptionsAsync_WhenOtherTenant_DoesNotMatch()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var t1Execution = Guid.NewGuid();
        var otherExecution = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedSubscriptionAsync(
            db, t1Execution, "t1-wait", "shared.topic", "k1", "statevia.event.subscribe.0", now);
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(CreateExecution(otherExecution, now, TestTenantIds.OtherTenantId));
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = otherExecution,
                NodeId = "other-wait",
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = ["statevia.event.subscribe.0"],
                CreatedAt = now
            });
            ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = Guid.NewGuid(),
                ExecutionId = otherExecution,
                NodeId = "other-wait",
                Topic = "shared.topic",
                CorrelationKey = "k1",
                ResumeEventName = "statevia.event.subscribe.0",
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }
        await using (var unfiltered = new CoreDbContext(
            db.Options,
            NullTenantContextAccessor.Instance,
            DisabledTenantQueryFilterOptions.Instance))
        {
            Assert.Equal(2, await unfiltered.Executions.CountAsync());
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var matches = await repo.ListMatchingSubscriptionsAsync(
            uow, TestTenantIds.T1TenantId, "shared.topic", "k1", CancellationToken.None);

        // Assert
        Assert.Single(matches);
        Assert.Equal(t1Execution, matches[0].ExecutionId);
    }

    private static ExecutionRow CreateExecution(Guid executionId, DateTime now, Guid? tenantId = null) =>
        new()
        {
            ExecutionId = executionId,
            TenantId = tenantId ?? TestTenantIds.T1TenantId,
            DefinitionId = Guid.NewGuid(),
            DefinitionVersionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        };

    private static async Task SeedSubscriptionAsync(
        InMemoryTestDatabase db,
        Guid executionId,
        string nodeId,
        string topic,
        string correlationKey,
        string resumeEventName,
        DateTime createdAt)
    {
        await using var ctx = new CoreDbContext(db.Options);
        if (!await ctx.Executions.AnyAsync(x => x.ExecutionId == executionId))
            ctx.Executions.Add(CreateExecution(executionId, createdAt));

        if (!await ctx.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId && x.NodeId == nodeId))
        {
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = nodeId,
                WaitKind = ExecutionWaitKind.EventWait,
                AllowedEvents = [resumeEventName],
                CreatedAt = createdAt
            });
        }

        ctx.ExecutionWaitSubscriptions.Add(new ExecutionWaitSubscriptionRow
        {
            SubscriptionId = Guid.NewGuid(),
            ExecutionId = executionId,
            NodeId = nodeId,
            Topic = topic,
            CorrelationKey = correlationKey,
            ResumeEventName = resumeEventName,
            CreatedAt = createdAt
        });
        await ctx.SaveChangesAsync();
    }
}
