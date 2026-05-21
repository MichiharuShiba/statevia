using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Services;

public sealed class DisplayIdServiceImplTests
{
    /// <summary>
    /// 識別子文字列が形式に合うとき同じ識別子を返す。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WhenIdOrUuidIsGuid_AndDisplayIdRowExists_ReturnsGuid()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

        // Act
        var uuid = Guid.NewGuid();
        await using (var ctx = new CoreDbContext(db.Options))
        {
            var now = DateTime.UtcNow;
            DefinitionTestData.AddDefinitionWithVersion(ctx, "t1", uuid, "def", createdAt: now);
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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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
        var sut = new DisplayIdServiceImpl(new TestCoreTransactionExecutor(new TestCoreUnitOfWorkFactory(db.Factory)));

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

