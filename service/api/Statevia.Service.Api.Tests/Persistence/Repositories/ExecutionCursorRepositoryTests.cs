using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ExecutionCursorRepository"/> の永続化シナリオ。</summary>
public sealed class ExecutionCursorRepositoryTests
{
    /// <summary>未存在 execution では cursor を新規挿入する。</summary>
    [Fact]
    public async Task UpsertAsync_Inserts_WhenMissing()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        await SeedRunningExecutionAsync(database, executionId);

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
        await using var uowDb = new CoreDbContext(database.Options);
        var uow = new CoreUnitOfWork(uowDb);
        await repo.UpsertAsync(uow, row, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(database.Options);
        var stored = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("node-a", stored.CurrentNodeId);
        Assert.Equal("Running", stored.State);
    }

    /// <summary>既存 cursor は upsert で上書きする。</summary>
    [Fact]
    public async Task UpsertAsync_Updates_WhenExists()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        await SeedRunningExecutionAsync(database, executionId, currentNodeId: "old");

        // Act
        await using var uowDb = new CoreDbContext(database.Options);
        var uow = new CoreUnitOfWork(uowDb);
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
        await using var verify = new CoreDbContext(database.Options);
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
        var now = DateTime.UtcNow;
        await SeedRunningExecutionAsync(database, executionId, currentNodeId: "old");

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

    /// <summary>クエリフィルタ有効かつテナント未解決でも既存 PK へ INSERT せず 1 行に収束する。</summary>
    [Fact]
    public async Task UpsertAsync_UpdatesExisting_WhenQueryFilterEnabled_AndTenantUnresolved()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = database.TenantAccessor;
        var options = database.Options;
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedRunningExecutionAsync(database, executionId, currentNodeId: "old");

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
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(options, accessor, DisabledTenantQueryFilterOptions.Instance);
        var stored = await verify.ExecutionCursors.IgnoreQueryFilters().SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("new", stored.CurrentNodeId);
    }

    /// <summary>同一 PK を 2 UoW から upsert しても 23505 にならず 1 行に収束する。</summary>
    [Fact]
    public async Task UpsertAsync_ConvergesToOneRow_WhenTwoUnitsOfWorkWriteSamePk()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        await SeedRunningExecutionAsync(database, executionId);

        await using var firstDb = new CoreDbContext(database.Options);
        await using var secondDb = new CoreDbContext(database.Options);
        var firstUow = new CoreUnitOfWork(firstDb);
        var secondUow = new CoreUnitOfWork(secondDb);

        // Act
        await repo.UpsertAsync(
            firstUow,
            new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "start",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            },
            CancellationToken.None);
        await repo.UpsertAsync(
            secondUow,
            new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = "wait",
                State = "Running",
                UpdatedAt = DateTime.UtcNow
            },
            CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(database.Options);
        var stored = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("wait", stored.CurrentNodeId);
    }

    /// <summary>DeleteAsync は cursor 行を削除する。</summary>
    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        await SeedRunningExecutionAsync(database, executionId, currentNodeId: "node-a");

        // Act
        await using var uowDb = new CoreDbContext(database.Options);
        var uow = new CoreUnitOfWork(uowDb);
        await repo.DeleteAsync(uow, executionId, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(database.Options);
        Assert.False(await verify.ExecutionCursors.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>GetByExecutionIdAsync は cursor 行を返す。</summary>
    [Fact]
    public async Task GetByExecutionIdAsync_ReturnsRow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var repo = new ExecutionCursorRepository();
        var executionId = Guid.NewGuid();
        await SeedRunningExecutionAsync(database, executionId, currentNodeId: "node-a");

        // Act
        await using var uowDb = new CoreDbContext(database.Options);
        var uow = new CoreUnitOfWork(uowDb);
        var row = await repo.GetByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("node-a", row!.CurrentNodeId);
    }

    /// <summary>実行行と任意の既存 cursor を投入する。</summary>
    private static async Task SeedRunningExecutionAsync(
        SqliteTestDatabase database,
        Guid executionId,
        string? currentNodeId = null)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var seed = new CoreDbContext(database.Options);
        ProjectTestData.AddDefaultProject(seed, TestTenantIds.T1TenantId, "t1", projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            seed,
            TestTenantIds.T1TenantId,
            definitionId,
            "wf-cursor",
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
        if (currentNodeId is not null)
        {
            seed.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                CurrentNodeId = currentNodeId,
                State = "Running",
                UpdatedAt = now
            });
        }

        await seed.SaveChangesAsync();
    }
}
