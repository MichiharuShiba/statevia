using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class ExecutionRepositoryTests
{
    /// <summary>
    /// 未存在の識別子では空値を返す。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var res = await repo.GetByIdAsync(uow, "t1", Guid.NewGuid(), default);
        // Assert
        Assert.Null(res);
    }

    /// <summary>GetByExecutionIdAsync はテナントフィルタなしで execution 行を返す。</summary>
    [Fact]
    public async Task GetByExecutionIdAsync_ReturnsRow_WithoutTenantFilter()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var executionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = "other-tenant",
                DefinitionId = Guid.NewGuid(),
                DefinitionVersionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.GetByExecutionIdAsync(uow, executionId, CancellationToken.None);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("other-tenant", row!.TenantId);
    }

    /// <summary>
    /// 開始時刻の降順で並べて表示用識別子を結合する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_OrdersByUpdatedAtDesc_AndUsesLeftJoin()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId1 = Guid.NewGuid();
        var wfId2 = Guid.NewGuid();

        var t1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = wfId1,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = t1,
                    UpdatedAt = t1,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = wfId2,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Completed",
                    StartedAt = t2,
                    UpdatedAt = t2,
                    CancelRequested = false,
                    RestartLost = false
                });

            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "execution",
                DisplayId = "WF-DISP-2",
                ResourceId = wfId2,
                CreatedAt = t2
            });

            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var (_, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: null,
                NameContains: null),
            default);
        Assert.Equal(2, items.Count);
        Assert.Equal("WF-DISP-2", items[0].DisplayId);
        Assert.Null(items[1].DisplayId);
        Assert.Equal(wfId2, items[0].Execution.ExecutionId);
    }

    /// <summary>
    /// ステータス条件で絞り込む の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByStatusFilter()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Completed",
                    StartedAt = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                });

            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: "Completed",
                DefinitionIdFilter: null,
                NameContains: null),
            default);
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("Completed", items[0].Execution.Status);
    }

    /// <summary>definitionId で 1 件に絞り込む。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByDefinitionId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var tenantId = "t1";
        var def1 = Guid.NewGuid();
        var def2 = Guid.NewGuid();
        var wf1 = Guid.NewGuid();
        var wf2 = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.AddRange(
                new ExecutionRow
                {
                    ExecutionId = wf1,
                    TenantId = tenantId,
                    DefinitionId = def1,
                    Status = "Running",
                    StartedAt = started,
                    UpdatedAt = started,
                    CancelRequested = false,
                    RestartLost = false
                },
                new ExecutionRow
                {
                    ExecutionId = wf2,
                    TenantId = tenantId,
                    DefinitionId = def2,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: def1,
                NameContains: null),
            default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wf1, items[0].Execution.ExecutionId);
    }

    /// <summary>name に displayId の部分一致のワークフローのみ含める。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByNameDisplayIdContains()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(
                new ExecutionRow
                {
                    ExecutionId = wfId,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = started,
                    UpdatedAt = started,
                    CancelRequested = false,
                    RestartLost = false
                });
            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "execution",
                DisplayId = "acme-orders-99",
                ResourceId = wfId,
                CreatedAt = started
            });
            var otherWf = Guid.NewGuid();
            ctx.Executions.Add(
                new ExecutionRow
                {
                    ExecutionId = otherWf,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new ExecutionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                StatusFilter: null,
                DefinitionIdFilter: null,
                NameContains: "orders"),
            default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wfId, items[0].Execution.ExecutionId);
    }

    /// <summary>
    /// 両方の行を永続化する の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task AddExecutionAndSnapshotAsync_PersistsBoth()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        var execution = new ExecutionRow
        {
            ExecutionId = wfId,
            TenantId = tenantId,
            DefinitionId = defId,
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        };
        var snapshot = new ExecutionGraphSnapshotRow
        {
            ExecutionId = wfId,
            GraphJson = "{\"nodes\":[]}",
            UpdatedAt = DateTime.UtcNow
        };

        await using var uow = await uowFactory.CreateAsync();
        await repo.AddExecutionAndSnapshotAsync(uow, execution, snapshot, default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        // Assert
        Assert.Equal(1, await ctx.Executions.CountAsync(x => x.ExecutionId == wfId));
        Assert.Equal(1, await ctx.ExecutionGraphSnapshots.CountAsync(x => x.ExecutionId == wfId));
    }

    /// <summary>
    /// スナップショットがないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task GetSnapshotByExecutionIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var wfId = Guid.NewGuid();
        // Assert
        await using var uow = await uowFactory.CreateAsync();
        var snapshot = await repo.GetSnapshotByExecutionIdAsync(uow, wfId, default);
        Assert.Null(snapshot);
    }

    /// <summary>
    /// 取消要求指定が空値なら既存値を維持して更新する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_UpdatesStatusAndGraphJson_AndKeepsCancelRequested_WhenNull()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = wfId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = wfId,
                GraphJson = "{\"nodes\":[1]}",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", null, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var w = await verify.Executions.FirstAsync(x => x.ExecutionId == wfId);
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.ExecutionId == wfId);
        Assert.Equal("Completed", w.Status);
        Assert.False(w.CancelRequested);
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>
    /// ワークフローが存在しない の場合は スナップショット更新は継続する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_WhenExecutionMissing_StillUpdatesSnapshot()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = wfId,
                GraphJson = "{\"nodes\":[1]}",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", true, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.ExecutionId == wfId);
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>
    /// スナップショットが存在しない の場合は 実行行のみ更新する。
    /// </summary>
    [Fact]
    public async Task UpdateExecutionAndSnapshotAsync_WhenSnapshotMissing_StillUpdatesExecutionOnly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new ExecutionRepository();

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = wfId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using var uow = await uowFactory.CreateAsync();
        await repo.UpdateExecutionAndSnapshotAsync(uow, wfId, "Completed", true, "{\"nodes\":[2]}", default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var w = await verify.Executions.FirstAsync(x => x.ExecutionId == wfId);
        Assert.Equal("Completed", w.Status);
        Assert.True(w.CancelRequested);

        var snapshotCount = await verify.ExecutionGraphSnapshots.CountAsync(x => x.ExecutionId == wfId);
        Assert.Equal(0, snapshotCount);
    }
}

