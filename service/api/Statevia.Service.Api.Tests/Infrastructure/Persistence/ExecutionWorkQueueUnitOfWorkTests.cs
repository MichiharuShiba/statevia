using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Service.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// UoW 参加型 <see cref="IExecutionWorkQueue.EnqueueManyAsync(ICoreUnitOfWork, IReadOnlyList{ExecutionWorkItemRow}, CancellationToken)"/> の検証。
/// </summary>
public sealed class ExecutionWorkQueueUnitOfWorkTests
{
    /// <summary>UoW 経由の enqueue は SaveChanges 後にのみ永続化される。</summary>
    [Fact]
    public async Task EnqueueManyAsync_WithUnitOfWork_PersistsOnlyAfterSaveChanges()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var queue = new ExecutionWorkQueue(db.Factory);
        var workItemId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = now,
            Attempts = 0,
            CreatedAt = now
        };

        // Act — SaveChanges 前は別コンテキストから見えない
        await using (var uow = await uowFactory.CreateAsync())
        {
            await queue.EnqueueManyAsync(uow, [item], CancellationToken.None);

            await using (var verifyBefore = new CoreDbContext(db.Options))
            {
                var beforeCount = await verifyBefore.ExecutionWorkItems.CountAsync(x => x.WorkItemId == workItemId);
                Assert.Equal(0, beforeCount);
            }

            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var verifyAfter = new CoreDbContext(db.Options);
        var after = await verifyAfter.ExecutionWorkItems.SingleAsync(x => x.WorkItemId == workItemId);
        Assert.Equal(executionId, after.ExecutionId);
        Assert.Equal(ExecutionWorkItemKinds.Start, after.Kind);
    }

    /// <summary>未完了 Start は execution_id 指定で lease 無しでも削除される。</summary>
    [Fact]
    public async Task CompleteIncompleteStartItemsAsync_WithUnitOfWork_DeletesStartItemsOnly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var queue = new ExecutionWorkQueue(db.Factory);
        var executionId = Guid.NewGuid();
        var otherExecutionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var startItem = new ExecutionWorkItemRow
        {
            WorkItemId = Guid.NewGuid(),
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = now,
            Attempts = 0,
            CreatedAt = now
        };
        var cancelItem = new ExecutionWorkItemRow
        {
            WorkItemId = Guid.NewGuid(),
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Cancel,
            Payload = "{}",
            AvailableAt = now,
            Attempts = 0,
            CreatedAt = now
        };
        var otherStart = new ExecutionWorkItemRow
        {
            WorkItemId = Guid.NewGuid(),
            ExecutionId = otherExecutionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = now,
            Attempts = 0,
            CreatedAt = now
        };

        await using (var seed = await uowFactory.CreateAsync())
        {
            await queue.EnqueueManyAsync(seed, [startItem, cancelItem, otherStart], CancellationToken.None);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using (var uow = await uowFactory.CreateAsync())
        {
            await queue.CompleteIncompleteStartItemsAsync(uow, executionId, CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var remaining = await verify.ExecutionWorkItems.ToListAsync();
        Assert.DoesNotContain(remaining, item => item.WorkItemId == startItem.WorkItemId);
        Assert.Contains(remaining, item => item.WorkItemId == cancelItem.WorkItemId);
        Assert.Contains(remaining, item => item.WorkItemId == otherStart.WorkItemId);
    }

    /// <summary>空リストは例外なく何もしない。</summary>
    [Fact]
    public async Task EnqueueManyAsync_WithUnitOfWork_EmptyList_NoOp()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var queue = new ExecutionWorkQueue(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await queue.EnqueueManyAsync(uow, [], CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        Assert.Equal(0, await verify.ExecutionWorkItems.CountAsync());
    }

    /// <summary>所有 miss 用 Release は lease を外し attempts を 1 戻す。</summary>
    [Fact]
    public async Task ReleaseWithoutCountingAttemptAsync_WhenLeaseOwnerMatches_DecrementsAttempts()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var queue = new ExecutionWorkQueue(db.Factory);
        var workItemId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        const string leaseOwner = "worker-a";
        var now = DateTime.UtcNow;
        var availableAt = now.AddSeconds(5);
        await SeedExecutionAsync(db, executionId);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Start,
            Payload = "{}",
            AvailableAt = now,
            LeaseOwner = leaseOwner,
            LeaseUntil = now.AddMinutes(1),
            Attempts = 4,
            CreatedAt = now
        };

        await using (var seed = await uowFactory.CreateAsync())
        {
            await queue.EnqueueManyAsync(seed, [item], CancellationToken.None);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await queue.ReleaseWithoutCountingAttemptAsync(workItemId, leaseOwner, availableAt, CancellationToken.None);

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var released = await verify.ExecutionWorkItems.SingleAsync(row => row.WorkItemId == workItemId);
        Assert.Null(released.LeaseOwner);
        Assert.Null(released.LeaseUntil);
        Assert.Equal(3, released.Attempts);
        Assert.Equal(availableAt, released.AvailableAt);
    }

    /// <summary>lease 所有者が違う Release は attempts を変えない。</summary>
    [Fact]
    public async Task ReleaseWithoutCountingAttemptAsync_WhenLeaseOwnerDiffers_DoesNotChangeAttempts()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var queue = new ExecutionWorkQueue(db.Factory);
        var workItemId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionAsync(db, executionId);
        var item = new ExecutionWorkItemRow
        {
            WorkItemId = workItemId,
            ExecutionId = executionId,
            Kind = ExecutionWorkItemKinds.Resume,
            Payload = "{}",
            AvailableAt = now,
            LeaseOwner = "worker-a",
            LeaseUntil = now.AddMinutes(1),
            Attempts = 4,
            CreatedAt = now
        };

        await using (var seed = await uowFactory.CreateAsync())
        {
            await queue.EnqueueManyAsync(seed, [item], CancellationToken.None);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await queue.ReleaseWithoutCountingAttemptAsync(
            workItemId,
            "worker-b",
            now.AddSeconds(5),
            CancellationToken.None);

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var unchanged = await verify.ExecutionWorkItems.SingleAsync(row => row.WorkItemId == workItemId);
        Assert.Equal("worker-a", unchanged.LeaseOwner);
        Assert.Equal(4, unchanged.Attempts);
    }

    /// <summary>SQLite の execution_work_items FK を満たすため、親 execution を投入する。</summary>
    private static async Task SeedExecutionAsync(SqliteTestDatabase db, Guid executionId)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var seed = db.Factory.CreateDbContext();
        ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            seed,
            TestTenantIds.DefaultTenantId,
            definitionId,
            "wf-work-queue",
            projectId,
            versionId: versionId);
        seed.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.DefaultTenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            Status = "Running",
            StartedAt = now,
            UpdatedAt = now,
            CancelRequested = false,
            RestartLost = false
        });
        await seed.SaveChangesAsync();
    }
}
