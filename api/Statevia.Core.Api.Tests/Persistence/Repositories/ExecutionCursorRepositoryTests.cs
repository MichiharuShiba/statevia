using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class ExecutionCursorRepositoryTests
{
    /// <summary>未存在 execution では cursor を新規挿入する。</summary>
    [Fact]
    public async Task UpsertAsync_Inserts_WhenMissing()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        var defId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                DefinitionId = defId,
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        var row = new ExecutionCursorRow
        {
            ExecutionId = executionId,
            TenantId = "t1",
            CurrentNodeId = "node-a",
            CurrentWorkerId = "worker-a",
            State = "Running",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpsertAsync(uow, row, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var stored = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("node-a", stored.CurrentNodeId);
        Assert.Equal("Running", stored.State);
    }

    /// <summary>既存 cursor は upsert で上書きする。</summary>
    [Fact]
    public async Task UpsertAsync_Updates_WhenExists()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                CurrentNodeId = "old",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.UpsertAsync(
            uow,
            new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                CurrentNodeId = "new",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            },
            CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var stored = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("new", stored.CurrentNodeId);
    }

    /// <summary>DeleteAsync は cursor 行を削除する。</summary>
    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.DeleteAsync(uow, executionId, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        Assert.False(await verify.ExecutionCursors.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>GetByExecutionIdAsync は cursor 行を返す。</summary>
    [Fact]
    public async Task GetByExecutionIdAsync_ReturnsRow()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = "t1",
                CurrentNodeId = "node-a",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.GetByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("node-a", row!.CurrentNodeId);
    }
}
