using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Security;
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
                TenantId = TestTenantIds.T1TenantId,
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
            TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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

    /// <summary>クエリフィルタ有効かつテナント解決済みなら既存 cursor を更新する（INSERT 重複を起こさない）。</summary>
    [Fact]
    public async Task UpsertAsync_UpdatesExisting_WhenQueryFilterEnabled_AndTenantResolved()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = database.TenantAccessor;
        var options = database.Options;
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var seed = new CoreDbContext(options, accessor, DisabledTenantQueryFilterOptions.Instance))
        {
            accessor.Set(null);
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.T1TenantId, "t1", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.T1TenantId,
                definitionId,
                "wf-cursor-filter",
                projectId,
                versionId: versionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = versionId,
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
            seed.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "old",
                State = "Running",
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        accessor.Set(TestTenantIds.T1Context);
        await using var uowDb = new CoreDbContext(options, accessor, EnabledTenantQueryFilterOptions.Instance);
        var uow = new CoreUnitOfWork(uowDb);

        // Act
        await repo.UpsertAsync(
            uow,
            new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "new",
                State = "Running",
                UpdatedAt = now
            },
            CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(options, accessor, DisabledTenantQueryFilterOptions.Instance);
        var stored = await verify.ExecutionCursors.IgnoreQueryFilters().SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("new", stored.CurrentNodeId);
    }

    /// <summary>クエリフィルタ有効かつテナント未解決では既存 cursor が見えず PK 重複になる。</summary>
    [Fact]
    public async Task UpsertAsync_Throws_WhenQueryFilterEnabled_AndTenantUnresolved()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = database.TenantAccessor;
        var options = database.Options;
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var seed = new CoreDbContext(options, accessor, DisabledTenantQueryFilterOptions.Instance))
        {
            accessor.Set(null);
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.T1TenantId, "t1", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.T1TenantId,
                definitionId,
                "wf-cursor-filter-unresolved",
                projectId,
                versionId: versionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = versionId,
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
            seed.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "old",
                State = "Running",
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        accessor.Set(null);
        await using var uowDb = new CoreDbContext(options, accessor, EnabledTenantQueryFilterOptions.Instance);
        var uow = new CoreUnitOfWork(uowDb);

        // Act
        await repo.UpsertAsync(
            uow,
            new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "new",
                State = "Running",
                UpdatedAt = now
            },
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync(CancellationToken.None));
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
                TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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
                TenantId = TestTenantIds.T1TenantId,
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
