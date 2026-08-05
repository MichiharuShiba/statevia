using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionBranchRepository"/> の永続化シナリオ。</summary>
public sealed class ExecutionBranchRepositoryTests
{
    /// <summary>InsertBranchesIdempotentAsync は新規行を挿入する。</summary>
    [Fact]
    public async Task InsertBranchesIdempotentAsync_InsertsNewRows()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionBranchRepository();
        var parentId = Guid.NewGuid();
        var childA = Guid.NewGuid();
        var childB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionsAsync(db.Options, now, parentId, childA, childB);

        var branches = new[]
        {
            CreateBranch(parentId, childA, "Start", "Join1", "A", now),
            CreateBranch(parentId, childB, "Start", "Join1", "B", now)
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.InsertBranchesIdempotentAsync(uow, branches, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var rows = await verify.ExecutionBranches
            .Where(x => x.ParentExecutionId == parentId)
            .OrderBy(x => x.BranchState)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal("A", rows[0].BranchState);
        Assert.Equal(childA, rows[0].ExecutionId);
        Assert.Equal("B", rows[1].BranchState);
        Assert.Equal(childB, rows[1].ExecutionId);
    }

    /// <summary>同一キー再挿入は execution_id 一致なら冪等に何もしない。</summary>
    [Fact]
    public async Task InsertBranchesIdempotentAsync_IsIdempotentWhenExecutionIdMatches()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionBranchRepository();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionsAsync(db.Options, now, parentId, childId);

        var branch = CreateBranch(parentId, childId, "Start", "Join1", "A", now);
        await using (var uow1 = await uowFactory.CreateAsync())
        {
            await repo.InsertBranchesIdempotentAsync(uow1, [branch], CancellationToken.None);
            await uow1.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow2 = await uowFactory.CreateAsync();
        await repo.InsertBranchesIdempotentAsync(uow2, [branch], CancellationToken.None);
        await uow2.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var count = await verify.ExecutionBranches.CountAsync(x => x.ParentExecutionId == parentId);
        Assert.Equal(1, count);
    }

    /// <summary>同一キーで異なる execution_id は衝突として失敗する。</summary>
    [Fact]
    public async Task InsertBranchesIdempotentAsync_ThrowsWhenExecutionIdConflicts()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionBranchRepository();
        var parentId = Guid.NewGuid();
        var child1 = Guid.NewGuid();
        var child2 = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionsAsync(db.Options, now, parentId, child1, child2);

        await using (var uow1 = await uowFactory.CreateAsync())
        {
            await repo.InsertBranchesIdempotentAsync(
                uow1,
                [CreateBranch(parentId, child1, "Start", "Join1", "A", now)],
                CancellationToken.None);
            await uow1.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow2 = await uowFactory.CreateAsync();
        var act = () => repo.InsertBranchesIdempotentAsync(
            uow2,
            [CreateBranch(parentId, child2, "Start", "Join1", "A", now)],
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    /// <summary>TryUpdateStatusAsync は status / output を更新する。</summary>
    [Fact]
    public async Task TryUpdateStatusAsync_UpdatesMatchingRow()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionBranchRepository();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionsAsync(db.Options, now, parentId, childId);

        await using (var uow1 = await uowFactory.CreateAsync())
        {
            await repo.InsertBranchesIdempotentAsync(
                uow1,
                [CreateBranch(parentId, childId, "Start", "Join1", "A", now)],
                CancellationToken.None);
            await uow1.SaveChangesAsync(CancellationToken.None);
        }

        var updatedAt = now.AddMinutes(1);

        // Act
        await using var uow2 = await uowFactory.CreateAsync();
        var updated = await repo.TryUpdateStatusAsync(
            uow2,
            parentId,
            "Start",
            "A",
            new ExecutionBranchStatusUpdate(
                ExecutionBranchStatuses.Completed,
                updatedAt,
                """{"ok":true}"""),
            CancellationToken.None);
        await uow2.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.True(updated);
        await using var verify = new CoreDbContext(db.Options);
        var row = await verify.ExecutionBranches.SingleAsync();
        Assert.Equal(ExecutionBranchStatuses.Completed, row.Status);
        Assert.Equal("""{"ok":true}""", row.OutputJson);
        Assert.Equal(updatedAt, row.UpdatedAt);
    }

    /// <summary>GetByChildExecutionIdAsync は子 ID で取得する。</summary>
    [Fact]
    public async Task GetByChildExecutionIdAsync_ReturnsMatchingRow()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionBranchRepository();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedExecutionsAsync(db.Options, now, parentId, childId);

        await using (var uow = await uowFactory.CreateAsync())
        {
            await repo.InsertBranchesIdempotentAsync(
                uow,
                [CreateBranch(parentId, childId, "Start", "Join1", "A", now)],
                CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var readUow = await uowFactory.CreateAsync();
        var found = await repo.GetByChildExecutionIdAsync(readUow, childId, CancellationToken.None);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(parentId, found.ParentExecutionId);
        Assert.Equal("Join1", found.JoinState);
    }

    private static ExecutionBranchRow CreateBranch(
        Guid parentId,
        Guid childId,
        string forkState,
        string joinState,
        string branchState,
        DateTime now) =>
        new()
        {
            ParentExecutionId = parentId,
            ExecutionId = childId,
            ForkState = forkState,
            JoinState = joinState,
            BranchState = branchState,
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static async Task SeedExecutionsAsync(
        DbContextOptions<CoreDbContext> options,
        DateTime now,
        params Guid[] executionIds)
    {
        await using var ctx = new CoreDbContext(options);
        foreach (var id in executionIds)
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = id,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
        }

        await ctx.SaveChangesAsync();
    }
}
