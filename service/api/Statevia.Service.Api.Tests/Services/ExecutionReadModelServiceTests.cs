using Statevia.Service.Api.Abstractions.Persistence;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionReadModelServiceTests
{
    private sealed class StubDisplayIdService : IDisplayIdService
    {
        public Guid? ExecutionResolveResult { get; set; }
        public Guid? DefinitionResolveResult { get; set; }

        public string? ExecutionDisplayId { get; set; }
        public string? DefinitionDisplayId { get; set; }

        

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            return kind switch
            {
                "execution" => Task.FromResult(ExecutionResolveResult),
                "definition" => Task.FromResult(DefinitionResolveResult),
                _ => Task.FromResult<Guid?>(null)
            };
        }

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            return Task.FromResult(kind switch
            {
                "execution" => ExecutionDisplayId,
                "definition" => DefinitionDisplayId,
                _ => null
            });
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static ExecutionReadModelService CreateSut(
        StubDisplayIdService display,
        TestCoreUnitOfWorkFactory factory) =>
        new(
            new TestCoreTransactionExecutor(factory),
            display,
            new FixedTenantContextAccessor(TestTenantIds.T1Context));

    /// <summary>
    /// 識別子解決に失敗したとき未検出例外を投げる。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_ThrowsNotFound_WhenResolveReturnsNull()
    {
        // Act & Assert
        using var db = new InMemoryTestDatabase();
        var display = new StubDisplayIdService { ExecutionResolveResult = null };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByDisplayIdAsync("DISP", CancellationToken.None));
    }

    /// <summary>
    /// ワークフローが存在しない の場合は 見つからない。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_ThrowsNotFound_WhenExecutionMissing()
    {
        // Act & Assert
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var display = new StubDisplayIdService { ExecutionResolveResult = uuid };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByDisplayIdAsync("DISP", CancellationToken.None));
    }

    /// <summary>
    /// スナップショットが存在しない の場合は 見つからない。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_ThrowsNotFound_WhenSnapshotMissing()
    {
        // Act & Assert
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService { ExecutionResolveResult = uuid };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByDisplayIdAsync("DISP", CancellationToken.None));
    }

    /// <summary>
    /// ステータスとノードをマップする の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_MapsStatusAndNodes()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        // Act
        var graphJson =
            "{\"nodes\":[" +
            "{\"NodeId\":\"n1\",\"StateName\":\"S1\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":null,\"Fact\":null}," +
            "{\"NodeId\":null,\"StateName\":\"S2\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":\"2020-01-01T00:00:00Z\",\"Fact\":\"Completed\"}," +
            "{\"NodeId\":\"n3\",\"StateName\":\"S3\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":\"2020-01-01T00:00:00Z\",\"Fact\":\"Failed\"}," +
            "{\"NodeId\":\"n4\",\"StateName\":\"S4\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":\"2020-01-01T00:00:00Z\",\"Fact\":\"Cancelled\"}," +
            "{\"NodeId\":\"n5\",\"StateName\":\"S5\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":\"2020-01-01T00:00:00Z\",\"Fact\":\"Joined\"}" +
            "]}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = "Cancelled",
                StartedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-2),
                CancelRequested = true,
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow.AddHours(-2)
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService
        {
            ExecutionResolveResult = uuid,
            ExecutionDisplayId = "EXEC-1",
            DefinitionDisplayId = null // fallback to execution.DefinitionId string
        };

        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));
        var res = await sut.GetByDisplayIdAsync("DISP", CancellationToken.None);

        // Assert
        Assert.Equal("EXEC-1", res.ExecutionId);
        Assert.Equal(definitionId.ToString(), res.GraphId);
        Assert.Equal("CANCELED", res.Status);
        Assert.NotNull(res.CanceledAt);
        Assert.Null(res.FailedAt);
        Assert.Null(res.CompletedAt);

        Assert.Equal(5, res.Nodes.Count);
        Assert.Equal("RUNNING", res.Nodes[0].Status);
        Assert.False(res.Nodes[0].CanceledByExecution);

        Assert.Equal(string.Empty, res.Nodes[1].ExecutionNodeId);
        Assert.Equal("SUCCEEDED", res.Nodes[1].Status);

        Assert.Equal("FAILED", res.Nodes[2].Status);
        Assert.False(res.Nodes[2].CanceledByExecution);

        Assert.Equal("CANCELED", res.Nodes[3].Status);
        Assert.True(res.Nodes[3].CanceledByExecution);

        Assert.Equal("SUCCEEDED", res.Nodes[4].Status);
    }

    /// <summary>
    /// 実行ステータスと時刻をマップする の挙動を確認する。
    /// </summary>
    [Theory]
    [InlineData("Running", "ACTIVE", false, false, false)]
    [InlineData("Cancelled", "CANCELED", true, false, false)]
    [InlineData("Failed", "FAILED", false, true, false)]
    [InlineData("Completed", "COMPLETED", false, false, true)]
    [InlineData("Paused", "UNKNOWN", false, false, false)]
    public async Task GetByDisplayIdAsync_MapsExecutionStatusAndTimestamps(
        string internalStatus,
        string expectedContractStatus,
        bool expectedCanceledAtNotNull,
        bool expectedFailedAtNotNull,
        bool expectedCompletedAtNotNull)
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        // Act
        var updatedAt = DateTime.UtcNow;
        var startedAt = DateTime.UtcNow.AddHours(-1);

        const string graphJson = "{\"nodes\":[{\"NodeId\":\"n1\",\"StateName\":null,\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":null,\"Fact\":null}]}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = internalStatus,
                StartedAt = startedAt,
                UpdatedAt = updatedAt,
                CancelRequested = internalStatus == "Cancelled",
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = graphJson,
                UpdatedAt = updatedAt
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService
        {
            ExecutionResolveResult = uuid,
            ExecutionDisplayId = "EXEC-1",
            DefinitionDisplayId = null // fallback to execution.DefinitionId string
        };

        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));
        var res = await sut.GetByDisplayIdAsync("DISP", CancellationToken.None);

        // Assert
        Assert.Equal(expectedContractStatus, res.Status);
        if (expectedCanceledAtNotNull)
            Assert.Equal(updatedAt, res.CanceledAt!.Value.UtcDateTime);
        else
            Assert.Null(res.CanceledAt);

        if (expectedFailedAtNotNull)
            Assert.Equal(updatedAt, res.FailedAt!.Value.UtcDateTime);
        else
            Assert.Null(res.FailedAt);

        if (expectedCompletedAtNotNull)
            Assert.Equal(updatedAt, res.CompletedAt!.Value.UtcDateTime);
        else
            Assert.Null(res.CompletedAt);
    }

    /// <summary>
    /// ノード配列が空のとき空のノード一覧を返す。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_MapNodes_ReturnsEmpty_WhenNodesArrayIsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        // Act
        const string graphJson = "{\"nodes\":[]}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService { ExecutionResolveResult = uuid, ExecutionDisplayId = "EXEC-1", DefinitionDisplayId = null };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        // Assert
        var res = await sut.GetByDisplayIdAsync("DISP", CancellationToken.None);
        Assert.Empty(res.Nodes);
    }

    /// <summary>
    /// 未知のFact値はノード状態をSUCCEEDEDとして扱う。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_MapNodeStatus_DefaultFallsBackToSucceeded()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        // Act
        var graphJson =
            "{\"nodes\":[" +
            "{\"NodeId\":\"n1\",\"StateName\":\"S1\",\"StartedAt\":\"2020-01-01T00:00:00Z\",\"CompletedAt\":\"2020-01-01T00:00:00Z\",\"Fact\":\"SomeOtherFact\"}" +
            "]}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = "Completed",
                StartedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = graphJson,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService { ExecutionResolveResult = uuid, ExecutionDisplayId = "EXEC-1", DefinitionDisplayId = null };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        // Assert
        var res = await sut.GetByDisplayIdAsync("DISP", CancellationToken.None);
        Assert.Single(res.Nodes);
        Assert.Equal("SUCCEEDED", res.Nodes[0].Status);
    }

    /// <summary>
    /// グラフ文字列が不正なとき空のノード一覧を返す。
    /// </summary>
    [Fact]
    public async Task GetByDisplayIdAsync_MapNodes_ReturnsEmpty_WhenGraphJsonInvalid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        // Act
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Executions.Add(new ExecutionRow
            {
                ExecutionId = uuid,
                TenantId = TestTenantIds.T1TenantId,
                DefinitionId = definitionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            ctx.ExecutionGraphSnapshots.Add(new ExecutionGraphSnapshotRow
            {
                ExecutionId = uuid,
                GraphJson = "not-json",
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var display = new StubDisplayIdService { ExecutionResolveResult = uuid, ExecutionDisplayId = "EXEC-2", DefinitionDisplayId = "G-1" };
        var sut = CreateSut(display, new TestCoreUnitOfWorkFactory(db.Factory));

        // Assert
        var res = await sut.GetByDisplayIdAsync("DISP", CancellationToken.None);
        Assert.Empty(res.Nodes);
    }
}

