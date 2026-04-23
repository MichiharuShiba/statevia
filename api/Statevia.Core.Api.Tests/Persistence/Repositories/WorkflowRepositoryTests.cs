using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class WorkflowRepositoryTests
{
    /// <summary>
    /// 未存在の識別子では空値を返す。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var res = await repo.GetByIdAsync("t1", Guid.NewGuid(), default);
        // Assert
        Assert.Null(res);
    }

    /// <summary>
    /// 開始時刻の降順で並べて表示用識別子を結合する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsAsync_OrdersByStartedAtDesc_AndUsesLeftJoin()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId1 = Guid.NewGuid();
        var wfId2 = Guid.NewGuid();

        var t1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.AddRange(
                new WorkflowRow
                {
                    WorkflowId = wfId1,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = t1,
                    UpdatedAt = t1,
                    CancelRequested = false,
                    RestartLost = false
                },
                new WorkflowRow
                {
                    WorkflowId = wfId2,
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
                Kind = "workflow",
                DisplayId = "WF-DISP-2",
                ResourceId = wfId2,
                CreatedAt = t2
            });

            await ctx.SaveChangesAsync();
        }

        // Assert
        var list = await repo.ListWithDisplayIdsAsync(tenantId, default);
        Assert.Equal(2, list.Count);
        Assert.Equal("WF-DISP-2", list[0].DisplayId);
        Assert.Null(list[1].DisplayId);
        Assert.Equal(wfId2, list[0].Workflow.WorkflowId);
    }

    /// <summary>
    /// ステータス条件で絞り込む の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByStatusFilter()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.AddRange(
                new WorkflowRow
                {
                    WorkflowId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                },
                new WorkflowRow
                {
                    WorkflowId = Guid.NewGuid(),
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Completed",
                    StartedAt = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = DateTime.UtcNow,
                    CancelRequested = false,
                    RestartLost = false
                });

            await ctx.SaveChangesAsync();
        }

        // Assert
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            tenantId, offset: 0, limit: 10, statusFilter: "Completed", definitionIdFilter: null, nameContains: null, default);
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("Completed", items[0].Workflow.Status);
    }

    /// <summary>definitionId で 1 件に絞り込む。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByDefinitionId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);
        var tenantId = "t1";
        var def1 = Guid.NewGuid();
        var def2 = Guid.NewGuid();
        var wf1 = Guid.NewGuid();
        var wf2 = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.AddRange(
                new WorkflowRow
                {
                    WorkflowId = wf1,
                    TenantId = tenantId,
                    DefinitionId = def1,
                    Status = "Running",
                    StartedAt = started,
                    UpdatedAt = started,
                    CancelRequested = false,
                    RestartLost = false
                },
                new WorkflowRow
                {
                    WorkflowId = wf2,
                    TenantId = tenantId,
                    DefinitionId = def2,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            tenantId, offset: 0, limit: 10, statusFilter: null, definitionIdFilter: def1, nameContains: null, default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wf1, items[0].Workflow.WorkflowId);
    }

    /// <summary>name に displayId の部分一致のワークフローのみ含める。</summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByNameDisplayIdContains()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();
        var started = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.Add(
                new WorkflowRow
                {
                    WorkflowId = wfId,
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
                Kind = "workflow",
                DisplayId = "acme-orders-99",
                ResourceId = wfId,
                CreatedAt = started
            });
            var otherWf = Guid.NewGuid();
            ctx.Workflows.Add(
                new WorkflowRow
                {
                    WorkflowId = otherWf,
                    TenantId = tenantId,
                    DefinitionId = defId,
                    Status = "Running",
                    StartedAt = started.AddDays(1),
                    UpdatedAt = started.AddDays(1),
                    CancelRequested = false,
                    RestartLost = false
                });
            await ctx.SaveChangesAsync();
        }

        // Act
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            tenantId, offset: 0, limit: 10, statusFilter: null, definitionIdFilter: null, nameContains: "orders", default);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(wfId, items[0].Workflow.WorkflowId);
    }

    /// <summary>
    /// 両方の行を永続化する の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task AddWorkflowAndSnapshotAsync_PersistsBoth()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        var workflow = new WorkflowRow
        {
            WorkflowId = wfId,
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
            WorkflowId = wfId,
            GraphJson = "{\"nodes\":[]}",
            UpdatedAt = DateTime.UtcNow
        };

        await repo.AddWorkflowAndSnapshotAsync(workflow, snapshot, default);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        // Assert
        Assert.Equal(1, await ctx.Workflows.CountAsync(x => x.WorkflowId == wfId));
        Assert.Equal(1, await ctx.ExecutionGraphSnapshots.CountAsync(x => x.WorkflowId == wfId));
    }

    /// <summary>
    /// スナップショットがないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task GetSnapshotByWorkflowIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var wfId = Guid.NewGuid();
        // Assert
        var snapshot = await repo.GetSnapshotByWorkflowIdAsync(wfId, default);
        Assert.Null(snapshot);
    }

    /// <summary>
    /// 取消要求指定が空値なら既存値を維持して更新する。
    /// </summary>
    [Fact]
    public async Task UpdateWorkflowAndSnapshotAsync_UpdatesStatusAndGraphJson_AndKeepsCancelRequested_WhenNull()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.Add(new WorkflowRow
            {
                WorkflowId = wfId,
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
                WorkflowId = wfId,
                GraphJson = "{\"nodes\":[1]}",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await ctx.SaveChangesAsync();
        }

        await repo.UpdateWorkflowAndSnapshotAsync(wfId, status: "Completed", cancelRequested: null, graphJson: "{\"nodes\":[2]}", default);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var w = await verify.Workflows.FirstAsync(x => x.WorkflowId == wfId);
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.WorkflowId == wfId);
        Assert.Equal("Completed", w.Status);
        Assert.False(w.CancelRequested);
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>
    /// ワークフローが存在しない の場合は スナップショット更新は継続する。
    /// </summary>
    [Fact]
    public async Task UpdateWorkflowAndSnapshotAsync_WhenWorkflowMissing_StillUpdatesSnapshot()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                WorkflowId = wfId,
                GraphJson = "{\"nodes\":[1]}",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await ctx.SaveChangesAsync();
        }

        await repo.UpdateWorkflowAndSnapshotAsync(wfId, status: "Completed", cancelRequested: true, graphJson: "{\"nodes\":[2]}", default);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var g = await verify.ExecutionGraphSnapshots.FirstAsync(x => x.WorkflowId == wfId);
        Assert.Equal("{\"nodes\":[2]}", g.GraphJson);
    }

    /// <summary>
    /// スナップショットが存在しない の場合は ワークフローのみ更新する。
    /// </summary>
    [Fact]
    public async Task UpdateWorkflowAndSnapshotAsync_WhenSnapshotMissing_StillUpdatesWorkflowOnly()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new WorkflowRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var wfId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.Add(new WorkflowRow
            {
                WorkflowId = wfId,
                TenantId = tenantId,
                DefinitionId = defId,
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        await repo.UpdateWorkflowAndSnapshotAsync(wfId, status: "Completed", cancelRequested: true, graphJson: "{\"nodes\":[2]}", default);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var w = await verify.Workflows.FirstAsync(x => x.WorkflowId == wfId);
        Assert.Equal("Completed", w.Status);
        Assert.True(w.CancelRequested);

        var snapshotCount = await verify.ExecutionGraphSnapshots.CountAsync(x => x.WorkflowId == wfId);
        Assert.Equal(0, snapshotCount);
    }
}

