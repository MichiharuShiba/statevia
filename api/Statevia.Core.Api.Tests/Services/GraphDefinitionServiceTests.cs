using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Services;

public sealed class GraphDefinitionServiceTests
{
    private sealed class StubDisplayIdService : IDisplayIdService
    {
        private readonly Guid? _resolveResult;
        public StubDisplayIdService(Guid? resolveResult) => _resolveResult = resolveResult;

        public Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            Task.FromResult(_resolveResult);

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// 識別子解決に失敗したとき未検出例外を投げる。
    /// </summary>
    [Fact]
    public async Task GetByGraphIdAsync_ThrowsNotFound_WhenResolveReturnsNull()
    {
        // Act & Assert
        using var db = new InMemoryTestDatabase();
        var sut = new GraphDefinitionService(db.Factory, new StubDisplayIdService(resolveResult: null));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByGraphIdAsync("G", "t1", CancellationToken.None));
    }

    /// <summary>
    /// 定義行が見つからないとき未検出例外を投げる。
    /// </summary>
    [Fact]
    public async Task GetByGraphIdAsync_ThrowsNotFound_WhenDefinitionRowMissing()
    {
        // Act & Assert
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var sut = new GraphDefinitionService(db.Factory, new StubDisplayIdService(uuid));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByGraphIdAsync("G", "t1", CancellationToken.None));
    }

    /// <summary>
    /// 変換済み定義からノード種別とエッジを期待どおりに構築する。
    /// </summary>
    [Fact]
    public async Task GetByGraphIdAsync_BuildsNodesAndEdgesWithExpectedNodeTypes()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var graphId = "graph-1";

        // Act
        var compiledJson =
            "{"
            + "\"name\":\"g1\","
            + "\"initialState\":\"StartState\","
            + "\"transitions\":{"
            + "\"StartState\":{\"Completed\":{\"next\":\"WaitState\",\"fork\":null,\"end\":false}},"
            + "\"WaitState\":{\"Completed\":{\"next\":\"ForkState\",\"fork\":null,\"end\":false}},"
            + "\"EndState\":{\"Completed\":{\"next\":null,\"fork\":null,\"end\":true}}"
            + "},"
            + "\"forkTable\":{\"ForkState\":[\"JoinState\"]},"
            + "\"joinTable\":{\"JoinState\":[\"WaitState\",\"ForkState\"]},"
            + "\"waitTable\":{\"WaitState\":\"UserApproved\"}"
            + "}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = uuid,
                TenantId = "t1",
                Name = "def",
                SourceYaml = "x",
                CompiledJson = compiledJson,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new GraphDefinitionService(db.Factory, new StubDisplayIdService(uuid));
        var res = await sut.GetByGraphIdAsync(graphId, "t1", CancellationToken.None);

        // Assert
        Assert.Equal(graphId, res.GraphId);
        Assert.NotNull(res.Ui);
        Assert.Equal("dagre", res.Ui!.Layout);

        var nodeById = new Dictionary<string, GraphNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in res.Nodes)
            nodeById[n.NodeId] = n;

        Assert.Equal("Start", nodeById["StartState"].NodeType);
        Assert.Equal("Wait", nodeById["WaitState"].NodeType);
        Assert.Equal("Fork", nodeById["ForkState"].NodeType);
        Assert.Equal("Join", nodeById["JoinState"].NodeType);
        Assert.Equal("End", nodeById["EndState"].NodeType);

        Assert.Contains(res.Edges, e => e.From == "StartState" && e.To == "WaitState");
        Assert.Contains(res.Edges, e => e.From == "WaitState" && e.To == "ForkState");
        Assert.Contains(res.Edges, e => e.From == "WaitState" && e.To == "JoinState");
        Assert.Contains(res.Edges, e => e.From == "ForkState" && e.To == "JoinState");
    }

    /// <summary>
    /// 終端遷移がない状態を通常ノードとして組み立てる。
    /// </summary>
    [Fact]
    public async Task GetByGraphIdAsync_BuildsTaskNodeType_WhenTransitionsExistButNoEndTargets()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var graphId = "graph-task";

        // Act
        var compiledJson =
            "{"
            + "\"name\":\"g1\","
            + "\"initialState\":\"StartState\","
            + "\"transitions\":{"
            + "\"StartState\":{\"Completed\":{\"next\":\"TaskState\",\"fork\":null,\"end\":false}},"
            + "\"TaskState\":{\"Completed\":{\"next\":null,\"fork\":null,\"end\":false}}"
            + "}"
            + "}";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = uuid,
                TenantId = "t1",
                Name = "def",
                SourceYaml = "x",
                CompiledJson = compiledJson,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new GraphDefinitionService(db.Factory, new StubDisplayIdService(uuid));
        var res = await sut.GetByGraphIdAsync(graphId, "t1", CancellationToken.None);

        var nodeById = new Dictionary<string, GraphNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in res.Nodes)
            nodeById[n.NodeId] = n;

        // Assert
        Assert.Equal("Start", nodeById["StartState"].NodeType);
        Assert.Equal("Task", nodeById["TaskState"].NodeType);
    }

    /// <summary>
    /// 変換済み定義が空値のとき空の構成要素を返す。
    /// </summary>
    [Fact]
    public async Task GetByGraphIdAsync_WhenCompiledJsonIsNull_ReturnsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uuid = Guid.NewGuid();
        var graphId = "graph-empty";

        // Act
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = uuid,
                TenantId = "t1",
                Name = "def",
                SourceYaml = "x",
                CompiledJson = "null",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new GraphDefinitionService(db.Factory, new StubDisplayIdService(uuid));
        var res = await sut.GetByGraphIdAsync(graphId, "t1", CancellationToken.None);

        // Assert
        Assert.Empty(res.Nodes);
        Assert.Empty(res.Edges);
    }
}

