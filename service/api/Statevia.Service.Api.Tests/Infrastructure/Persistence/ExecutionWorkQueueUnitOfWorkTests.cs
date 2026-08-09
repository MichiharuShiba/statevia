using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

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
}
