using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionOperationalProjectionSyncTests
{
    /// <summary>Wait ノード（EventWait）のみ execution_waits に永続化する。</summary>
    [Fact]
    public async Task SyncAsync_PersistsEventWaitAndCursor_FromGraphJson()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var graphJson =
            """
            {"nodes":[
              {"nodeId":"wait1","nodeName":"Ask","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":"Approve","workerId":"w1"},
              {"nodeId":"task1","nodeName":"Work","nodeType":"Task","startedAt":"2026-05-26T00:00:01Z","completedAt":"2026-05-26T00:00:02Z","fact":"Completed"}
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
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.FindAsync(executionId);
        Assert.NotNull(cursor);
        Assert.Equal("wait1", cursor!.CurrentNodeId);
        Assert.Equal("w1", cursor.CurrentWorkerId);

        var wait = await verify.ExecutionWaits.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal(ExecutionWaitKind.EventWait, wait.WaitKind);
        Assert.Equal(["Approve"], wait.AllowedEvents);
    }

    /// <summary>複数イベント Wait は allowedEvents をそのまま永続化する（waitKey 不要）。</summary>
    [Fact]
    public async Task SyncAsync_PersistsMultiEventWait_FromAllowedEvents()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var graphJson =
            """
            {"nodes":[
              {"nodeId":"wait-multi","nodeName":"ApproveTask","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":null,"allowedEvents":["approve","reject"],"workerId":"w1"}
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
                ActiveStates = ["ApproveTask"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            graphJson,
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var wait = await verify.ExecutionWaits.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("wait-multi", wait.NodeId);
        Assert.Equal(["approve", "reject"], wait.AllowedEvents);
    }

    /// <summary>
    /// 許可イベント未設定の Wait は cursor 優先候補にせず、他の実行中ノードを選ぶ。
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsWaitWithoutEvents_WhenSelectingCursor()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        var graphJson =
            """
            {"nodes":[
              {"nodeId":"wait-empty","nodeName":"BrokenWait","nodeType":"Wait","startedAt":"2026-05-26T00:00:02Z","waitKey":null,"allowedEvents":[],"workerId":"w-wait"},
              {"nodeId":"task1","nodeName":"Prepare","nodeType":"Task","startedAt":"2026-05-26T00:00:01Z","workerId":"worker-prepare"}
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
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, new ExecutionCursorRepository(), new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()), request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("task1", cursor.CurrentNodeId);
        Assert.Equal("worker-prepare", cursor.CurrentWorkerId);
        Assert.False(await verify.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>終了状態では cursor / wait を削除する。</summary>
    [Fact]
    public async Task SyncAsync_ClearsOperationalRows_WhenTerminal()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await SeedExecutionAsync(db, executionId);
        await using (var ctx = new CoreDbContext(db.Options))
        {
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
                AllowedEvents = ["Approve"],
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
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        Assert.Null(await verify.ExecutionCursors.FindAsync(executionId));
        Assert.False(await verify.ExecutionWaits.AnyAsync(x => x.ExecutionId == executionId));
    }

    /// <summary>NodeIdToClear で一致 wait を先行削除する。</summary>
    [Fact]
    public async Task SyncAsync_DeletesMatchingWait_WhenNodeIdToClearProvided()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var cursorRepo = new ExecutionCursorRepository();
        var waitRepo = new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator());
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
                    AllowedEvents = ["Approve"],
                    CreatedAt = now
                },
                new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = "wait-other",
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = ["Other"],
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
              {"nodeId":"wait-other","nodeName":"Other","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","waitKey":"Other","workerId":"w2"}
            ]}
            """,
            NodeIdToClear: "wait-old");

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, cursorRepo, waitRepo, request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var nodeIds = await verify.ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .Select(x => x.NodeId)
            .ToListAsync();
        Assert.Single(nodeIds);
        Assert.Equal("wait-other", nodeIds[0]);
    }

    /// <summary>Wait 以外の実行中ノードは ActiveStates から cursor 位置を選ぶ。</summary>
    [Fact]
    public async Task SyncAsync_SelectsActiveStateNode_WhenRunningTaskMatchesSnapshot()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        var graphJson =
            """
            {"nodes":[
              {"nodeId":"task1","nodeName":"Prepare","nodeType":"Task","startedAt":"2026-05-26T00:00:01Z","workerId":"worker-prepare"}
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
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, new ExecutionCursorRepository(), new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()), request, new DefaultIdGenerator(), CancellationToken.None);
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
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        await SeedExecutionAsync(db, executionId);

        var graphJson =
            """
            {"nodes":[
              {"nodeId":"task-old","nodeName":"A","nodeType":"Task","startedAt":"2026-05-26T00:00:00Z","workerId":"w-old"},
              {"nodeId":"task-new","nodeName":"B","nodeType":"Task","startedAt":"2026-05-26T00:00:02Z","workerId":"w-new"}
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
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(uow, new ExecutionCursorRepository(), new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()), request, new DefaultIdGenerator(), CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var cursor = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("task-new", cursor.CurrentNodeId);
        Assert.Equal("w-new", cursor.CurrentWorkerId);
    }

    /// <summary>wait.subscribe のグラフでも durable wait と購読行が残る。</summary>
    [Fact]
    public async Task SyncAsync_PersistsSubscribeWaitAndSubscriptions_FromGraphJson()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var executionId = Guid.NewGuid();
        var graphJson =
            """
            {"nodes":[
              {"nodeId":"wait-sub","nodeName":"Sub","nodeType":"Wait","startedAt":"2026-05-26T00:00:00Z","allowedEvents":["statevia.event.subscribe.0"],"subscriptions":[{"topic":"order.created","key":"k1","resumeEventName":"statevia.event.subscribe.0"}],"workerId":"w-sub"}
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
                ActiveStates = ["Sub"],
                IsCompleted = false,
                IsCancelled = false,
                IsFailed = false
            },
            graphJson,
            NodeIdToClear: null);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await ExecutionOperationalProjectionSync.SyncAsync(
            uow,
            new ExecutionCursorRepository(),
            new ExecutionWaitRepository(db.Factory, new DefaultIdGenerator()),
            request,
            new DefaultIdGenerator(),
            CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var verify = new CoreDbContext(db.Options);
        var wait = await verify.ExecutionWaits.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("wait-sub", wait.NodeId);
        Assert.Equal(["statevia.event.subscribe.0"], wait.AllowedEvents);
        var subscription = await verify.ExecutionWaitSubscriptions.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("order.created", subscription.Topic);
        Assert.Equal("k1", subscription.CorrelationKey);
        var cursor = await verify.ExecutionCursors.SingleAsync(x => x.ExecutionId == executionId);
        Assert.Equal("wait-sub", cursor.CurrentNodeId);
    }

    private static async Task SeedExecutionAsync(SqliteTestDatabase db, Guid executionId)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var ctx = new CoreDbContext(db.Options);
        ProjectTestData.AddDefaultProject(ctx, TestTenantIds.T1TenantId, "t1", projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            TestTenantIds.T1TenantId,
            definitionId,
            "wf-ops-sync",
            projectId,
            versionId: versionId);
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TestTenantIds.T1TenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CancelRequested = false,
            RestartLost = false
        });
        await ctx.SaveChangesAsync();
    }
}
