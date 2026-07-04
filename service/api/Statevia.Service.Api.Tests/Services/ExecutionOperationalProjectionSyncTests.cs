using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionOperationalProjectionSyncTests
{
    /// <summary>Wait ノード（EventWait）のみ execution_waits に永続化する。</summary>
    [Fact]
    public async Task SyncAsync_PersistsEventWaitAndCursor_FromGraphJson()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository();
        var executionId = Guid.NewGuid();
        var graphJson =
            """
            {"nodes":[
              {"nodeId":"wait1","stateName":"Ask","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":"Approve","workerId":"w1"},
              {"nodeId":"task1","stateName":"Work","nodeType":"Task","startedAt":"2026-05-26T00:00:01Z","completedAt":"2026-05-26T00:00:02Z","fact":"Completed"}
            ]}
            """;

        await SeedExecutionAsync(db, executionId);

        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            TestTenantIds.T1TenantId,
            "Running",
            new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["Ask"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            graphJson,
            ResumeTokenToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.FindAsync(executionId);
        Assert.NotNull(cursor);
        Assert.Equal("wait1", cursor!.CurrentNodeId);
        Assert.Equal("w1", cursor.CurrentWorkerId);

        var wait = await verify.ExecutionWaits.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal(ExecutionWaitKind.EventWait, wait.WaitKind);
        Assert.Equal("Approve", wait.ResumeToken);
    }

    /// <summary>終了状態では cursor / wait を削除する。</summary>
    [Fact]
    public async Task SyncAsync_ClearsOperationalRows_WhenTerminal()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository();
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
                Status = "Completed",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionCursors.Add(new ExecutionCursorRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.T1TenantId,
                State = "Running",
                UpdatedAt = now
            });
            ctx.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = "n1",
                WaitKind = ExecutionWaitKind.EventWait,
                ResumeToken = "Approve",
                CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            TestTenantIds.T1TenantId,
            "Completed",
            Snapshot: null,
            GraphJson: "{}",
            ResumeTokenToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        Assert.Null(await verify.ExecutionCursors.FindAsync(executionId));
        Assert.False(await verify.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>ResumeTokenToClear で一致 wait を先行削除する。</summary>
    [Fact]
    public async Task SyncAsync_DeletesMatchingWait_WhenResumeTokenToClearProvided()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await SeedExecutionAsync(db, executionId);
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.ExecutionWaits.AddRange(
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "wait-old",
                    WaitKind = ExecutionWaitKind.EventWait,
                    ResumeToken = "Approve",
                    CreatedAt = now
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "wait-other",
                    WaitKind = ExecutionWaitKind.EventWait,
                    ResumeToken = "Other",
                    CreatedAt = now
                });
            await ctx.SaveChangesAsync();
        }

        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            TestTenantIds.T1TenantId,
            "Running",
            Snapshot: null,
            GraphJson:
            """
            {"nodes":[
              {"nodeId":"wait-other","stateName":"Other","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":"Other","workerId":"w2"}
            ]}
            """,
            ResumeTokenToClear: "Approve");

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var tokens = await verify.ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .Select(x => x.ResumeToken)
            .ToListAsync();
        Assert.Single(tokens);
        Assert.Equal("Other", tokens[0]);
    }

    /// <summary>Wait 以外の実行中ノードは ActiveStates から cursor 位置を選ぶ。</summary>
    [Fact]
    public async Task SyncAsync_SelectsActiveStateNode_WhenRunningTaskMatchesSnapshot()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        var graphJson =
            """
            {"nodes":[
              {"nodeId":"task1","stateName":"Prepare","nodeType":"Task","startedAt":"2026-05-26T00:00:01Z","workerId":"worker-prepare"}
            ]}
            """;
        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            TestTenantIds.T1TenantId,
            "Running",
            new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = ["Prepare"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            graphJson,
            ResumeTokenToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(
            uow,
            new ExecutionCursorRepository(),
            new ExecutionWaitRepository(),
            request,
            CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("task1", cursor.CurrentNodeId);
        Assert.Equal("worker-prepare", cursor.CurrentWorkerId);
        Assert.False(await verify.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>ActiveStates が空のときは startedAt 最新の実行中ノードを cursor にする。</summary>
    [Fact]
    public async Task SyncAsync_SelectsLatestRunningNode_WhenSnapshotHasNoActiveStates()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        var graphJson =
            """
            {"nodes":[
              {"nodeId":"task-old","stateName":"A","nodeType":"Task","startedAt":"2026-05-26T00:00:00Z","workerId":"w-old"},
              {"nodeId":"task-new","stateName":"B","nodeType":"Task","startedAt":"2026-05-26T00:00:02Z","workerId":"w-new"}
            ]}
            """;
        var request = new ExecutionOperationalProjectionSyncRequest(
            executionId,
            TestTenantIds.T1TenantId,
            "Running",
            new ExecutionSnapshot
            {
                ExecutionId = executionId.ToString(),
                WorkflowName = "wf",
                ActiveStates = Array.Empty<string>(),
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            graphJson,
            ResumeTokenToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(
            uow,
            new ExecutionCursorRepository(),
            new ExecutionWaitRepository(),
            request,
            CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("task-new", cursor.CurrentNodeId);
        Assert.Equal("w-new", cursor.CurrentWorkerId);
    }

    private static async Task SeedExecutionAsync(InMemoryTestDatabase db, Guid executionId)
    {
        await using var ctx = new CoreDbContext(db.Options);
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
        await ctx.SaveChangesAsync();
    }
}
