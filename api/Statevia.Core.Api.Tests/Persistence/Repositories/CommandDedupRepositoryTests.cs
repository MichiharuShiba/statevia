using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class CommandDedupRepositoryTests
{
    /// <summary>
    /// キーが存在しないとき空値を返す。
    /// </summary>
    [Fact]
    public async Task FindValidAsync_ReturnsNull_WhenKeyMissing()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.FindValidAsync(uow, "missing", DateTime.UtcNow, default);

        // Assert
        Assert.Null(row);
    }

    /// <summary>
    /// 期限切れ行は空値を返す。
    /// </summary>
    [Fact]
    public async Task FindValidAsync_ReturnsNull_WhenExpired()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();
        var dedupKey = "tenant|POST /v1/executions:abc123";
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "abc123",
                RequestHash = null,
                StatusCode = 201,
                ResponseBody = "{\"ok\":true}",
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(-1)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.FindValidAsync(uow, dedupKey, now, default);

        // Assert
        Assert.Null(row);
    }

    /// <summary>
    /// 期限内の行は一致データを返す。
    /// </summary>
    [Fact]
    public async Task FindValidAsync_ReturnsRow_WhenNotExpired()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();
        var dedupKey = "tenant|POST /v1/executions:abc123";
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "abc123",
                RequestHash = "hash-1",
                StatusCode = 201,
                ResponseBody = "{\"ok\":true}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var row = await repo.FindValidAsync(uow, dedupKey, now, default);

        // Assert
        Assert.NotNull(row);
        Assert.Equal("hash-1", row!.RequestHash);
    }

    /// <summary>
    /// 行を永続化する挙動を確認する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_PersistsRow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();
        var now = DateTime.UtcNow;
        var row = new CommandDedupRow
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions",
            IdempotencyKey = "idem",
            RequestHash = "h",
            StatusCode = 201,
            ResponseBody = "{\"ok\":true}",
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.SaveAsync(uow, row, default);
        await uow.SaveChangesAsync(CancellationToken.None);

        await using var verify = new CoreDbContext(db.Options);

        // Assert
        var saved = await verify.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k1");
        Assert.NotNull(saved);
        Assert.Equal("idem", saved!.IdempotencyKey);
    }

    /// <summary>
    /// 同一 UoW への追加は SaveChanges 前に永続化されない。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithDb_AddsWithoutSaveChanges()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();

        await using var uow = await uowFactory.CreateAsync();
        var row = new CommandDedupRow
        {
            DedupKey = "k2",
            Endpoint = "POST /v1/executions",
            IdempotencyKey = "idem2",
            RequestHash = null,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        await repo.SaveAsync(uow, row, default);

        await using var verify = new CoreDbContext(db.Options);

        // Assert
        Assert.Null(await verify.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k2"));

        await uow.SaveChangesAsync(CancellationToken.None);
        await using var verify2 = new CoreDbContext(db.Options);
        Assert.NotNull(await verify2.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k2"));
    }

    /// <summary>
    /// 同一冪等キーでリクエストハッシュが異なる有効行があるとき、その行を返す。
    /// </summary>
    [Fact]
    public async Task FindValidConflictingRequestHashAsync_ReturnsRow_WhenSameIdempotencyKeyButDifferentHash()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();
        var now = DateTime.UtcNow;
        const string tenantId = "default";
        const string idem = "idem-e2e-test";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = $"{tenantId}|POST /v1/executions:{idem}:AAA",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = idem,
                RequestHash = "AAA",
                StatusCode = 201,
                ResponseBody = "{}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var conflict = await repo.FindValidConflictingRequestHashAsync(
            uow,
            tenantId,
            "POST /v1/executions",
            idem,
            "BBB",
            now,
            default);

        // Assert
        Assert.NotNull(conflict);
        Assert.Equal("AAA", conflict!.RequestHash);
    }

    /// <summary>
    /// リクエストハッシュが一致するときは空値を返す。
    /// </summary>
    [Fact]
    public async Task FindValidConflictingRequestHashAsync_ReturnsNull_WhenHashMatches()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new CommandDedupRepository();
        var now = DateTime.UtcNow;
        const string tenantId = "default";
        const string hash = "SAME";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = $"{tenantId}|POST /v1/executions:key1:{hash}",
                Endpoint = "POST /v1/executions",
                IdempotencyKey = "key1",
                RequestHash = hash,
                StatusCode = 201,
                ResponseBody = "{}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var conflict = await repo.FindValidConflictingRequestHashAsync(
            uow,
            tenantId,
            "POST /v1/executions",
            "key1",
            hash,
            now,
            default);

        // Assert
        Assert.Null(conflict);
    }
}
