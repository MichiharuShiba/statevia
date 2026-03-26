using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Services;

public sealed class DisplayIdServiceImplTests
{
    private sealed class ThrowingSaveChangesDbContext : CoreDbContext
    {
        private readonly Func<bool> _shouldThrow;

        public ThrowingSaveChangesDbContext(DbContextOptions<CoreDbContext> options, Func<bool> shouldThrow)
            : base(options) => _shouldThrow = shouldThrow;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_shouldThrow())
                throw new DbUpdateException("Simulated DbUpdateException during testing.");

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class ThrowingSaveChangesDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<CoreDbContext>
    {
        private readonly DbContextOptions<CoreDbContext> _options;
        private readonly Func<bool> _shouldThrow;
        private int _saveCalls;

        public ThrowingSaveChangesDbContextFactory(DbContextOptions<CoreDbContext> options, bool throwAlways)
        {
            _options = options;
            _shouldThrow = () =>
            {
                _saveCalls++;
                if (throwAlways) return true;
                return _saveCalls == 1;
            };
        }

        public int SaveCalls => _saveCalls;

        public CoreDbContext CreateDbContext() => new ThrowingSaveChangesDbContext(_options, _shouldThrow);

        public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    /// <summary>
    /// 表示用識別子を発番して保存する。
    /// </summary>
    [Fact]
    public async Task AllocateAsync_ReturnsDisplayId_AndPersistsRow()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        var displayId = await sut.AllocateAsync("definition", uuid, CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var row = await verify.DisplayIds.FirstOrDefaultAsync(x => x.Kind == "definition" && x.ResourceId == uuid);
        Assert.NotNull(row);
        Assert.Equal(displayId, row!.DisplayId);
    }

    /// <summary>
    /// 保存が一度失敗しても再試行で発番に成功する。
    /// </summary>
    [Fact]
    public async Task AllocateAsync_WhenSaveChangesThrowsOnce_RetriesAndReturnsDisplayId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var factory = new ThrowingSaveChangesDbContextFactory(db.Options, throwAlways: false);
        var sut = new DisplayIdServiceImpl(factory);

        // Act
        var uuid = Guid.NewGuid();
        var displayId = await sut.AllocateAsync("definition", uuid, CancellationToken.None);

        // Assert
        Assert.Equal(2, factory.SaveCalls);

        await using var verify = new CoreDbContext(db.Options);
        var row = await verify.DisplayIds.FirstOrDefaultAsync(x => x.Kind == "definition" && x.ResourceId == uuid);
        Assert.NotNull(row);
        Assert.Equal(displayId, row!.DisplayId);
    }

    /// <summary>
    /// 保存が連続失敗したとき最大試行後に操作不能例外を投げる。
    /// </summary>
    [Fact]
    public async Task AllocateAsync_WhenSaveChangesAlwaysThrows_ThrowsInvalidOperationAfterMaxAttempts()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var factory = new ThrowingSaveChangesDbContextFactory(db.Options, throwAlways: true);
        var sut = new DisplayIdServiceImpl(factory);

        // Act
        var uuid = Guid.NewGuid();

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.AllocateAsync("definition", uuid, CancellationToken.None));

        Assert.Contains("Failed to allocate display_id", ex.Message);
        Assert.Equal(10, factory.SaveCalls); // maxAttempts = 10
    }

    /// <summary>
    /// 識別子文字列が形式に合うとき同じ識別子を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenIdOrUuidIsGuid_AndDisplayIdRowExists_ReturnsGuid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.DisplayIds.Add(new DisplayIdRow { Kind = "definition", DisplayId = "DISP", ResourceId = uuid, CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var resolved = await sut.ResolveAsync("definition", uuid.ToString(), CancellationToken.None);
        Assert.Equal(uuid, resolved);
    }

    /// <summary>
    /// 定義種別で定義行が存在するとき識別子を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenKindDefinitionAndDefinitionRowExists_ReturnsGuid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = uuid,
                TenantId = "t1",
                Name = "def",
                SourceYaml = "x",
                CompiledJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var resolved = await sut.ResolveAsync("definition", uuid.ToString(), CancellationToken.None);
        Assert.Equal(uuid, resolved);
    }

    /// <summary>
    /// 実行種別で実行行が存在するとき識別子を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenKindWorkflowAndWorkflowRowExists_ReturnsGuid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.Workflows.Add(new WorkflowRow
            {
                WorkflowId = uuid,
                TenantId = "t1",
                DefinitionId = Guid.NewGuid(),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var resolved = await sut.ResolveAsync("workflow", uuid.ToString(), CancellationToken.None);
        Assert.Equal(uuid, resolved);
    }

    /// <summary>
    /// 不明種別で一致行がないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenKindUnknownAndGuid_AndNothingExists_ReturnsNull()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        // Assert
        var resolved = await sut.ResolveAsync("unknown-kind", uuid.ToString(), CancellationToken.None);
        Assert.Null(resolved);
    }

    /// <summary>
    /// 表示用識別子入力時は対応する資源識別子を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenIdOrUuidIsDisplayId_ReturnsResourceId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.DisplayIds.Add(new DisplayIdRow { Kind = "definition", DisplayId = "MY-DISP", ResourceId = uuid, CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var resolved = await sut.ResolveAsync("definition", "MY-DISP", CancellationToken.None);
        Assert.Equal(uuid, resolved);
    }

    /// <summary>
    /// 形式不一致の入力はそのまま返す。
    /// </summary>
    [Fact]
    public async Task GetDisplayIdAsync_ReturnsInput_WhenIdOrUuidIsNotGuid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var res = await sut.GetDisplayIdAsync("definition", "not-a-guid", CancellationToken.None);
        // Assert
        Assert.Equal("not-a-guid", res);
    }

    /// <summary>
    /// 一致行があるとき表示用識別子を返す。
    /// </summary>
    [Fact]
    public async Task GetDisplayIdAsync_ReturnsDisplayId_WhenFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.DisplayIds.Add(new DisplayIdRow { Kind = "definition", DisplayId = "DISP-1", ResourceId = uuid, CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var res = await sut.GetDisplayIdAsync("definition", uuid.ToString(), CancellationToken.None);
        Assert.Equal("DISP-1", res);
    }

    /// <summary>
    /// 一致行がないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task GetDisplayIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var uuid = Guid.NewGuid();
        // Assert
        var res = await sut.GetDisplayIdAsync("definition", uuid.ToString(), CancellationToken.None);
        Assert.Null(res);
    }

    /// <summary>
    /// 入力集合が空のとき空の辞書を返す。
    /// </summary>
    [Fact]
    public async Task GetDisplayIdsAsync_ReturnsEmpty_WhenInputIsEmpty()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var dict = await sut.GetDisplayIdsAsync("definition", Array.Empty<Guid>(), CancellationToken.None);
        // Assert
        Assert.Empty(dict);
    }

    /// <summary>
    /// 一部の行が存在する の場合は 辞書を返す。
    /// </summary>
    [Fact]
    public async Task GetDisplayIdsAsync_ReturnsDictionary_WhenSomeRowsExist()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(db.Factory);

        // Act
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.DisplayIds.AddRange(
                new DisplayIdRow { Kind = "definition", DisplayId = "D1", ResourceId = id1, CreatedAt = DateTime.UtcNow },
                new DisplayIdRow { Kind = "definition", DisplayId = "D2", ResourceId = id2, CreatedAt = DateTime.UtcNow }
            );
            await ctx.SaveChangesAsync();
        }

        // Assert
        var dict = await sut.GetDisplayIdsAsync("definition", new[] { id1, id1, id2 }, CancellationToken.None);
        Assert.Equal(new Dictionary<Guid, string> { [id1] = "D1", [id2] = "D2" }, dict);
    }
}

