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
        var repo = new CommandDedupRepository(db.Factory);

        // Act
        var row = await repo.FindValidAsync("missing", DateTime.UtcNow, default);
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
        var repo = new CommandDedupRepository(db.Factory);

        // Act
        var dedupKey = "tenant|POST /v1/workflows:abc123";
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "abc123",
                RequestHash = null,
                StatusCode = 201,
                ResponseBody = "{\"ok\":true}",
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(-1)
            });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var row = await repo.FindValidAsync(dedupKey, now, default);
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
        var repo = new CommandDedupRepository(db.Factory);

        // Act
        var dedupKey = "tenant|POST /v1/workflows:abc123";
        var now = DateTime.UtcNow;

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = dedupKey,
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "abc123",
                RequestHash = "hash-1",
                StatusCode = 201,
                ResponseBody = "{\"ok\":true}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync();
        }

        // Assert
        var row = await repo.FindValidAsync(dedupKey, now, default);
        Assert.NotNull(row);
        Assert.Equal("hash-1", row!.RequestHash);
    }

    /// <summary>
    /// 行を永続化する の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_PersistsRow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new CommandDedupRepository(db.Factory);

        // Act
        var now = DateTime.UtcNow;
        var row = new CommandDedupRow
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/workflows",
            IdempotencyKey = "idem",
            RequestHash = "h",
            StatusCode = 201,
            ResponseBody = "{\"ok\":true}",
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        await repo.SaveAsync(row, default);

        await using var verify = new CoreDbContext(db.Options);
        // Assert
        var saved = await verify.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k1");
        Assert.NotNull(saved);
        Assert.Equal("idem", saved!.IdempotencyKey);
    }

    /// <summary>
    /// 既存文脈への追加は保存前に永続化されない。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithDb_AddsWithoutSaveChanges()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var repo = new CommandDedupRepository(db.Factory);

        // Act
        await using var ctx = new CoreDbContext(db.Options);
        var row = new CommandDedupRow
        {
            DedupKey = "k2",
            Endpoint = "POST /v1/workflows",
            IdempotencyKey = "idem2",
            RequestHash = null,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await repo.SaveAsync(ctx, row, default);

        // SaveChangesAsync していないので、まだ永続化されていない（db コンテキスト外では見えない）
        await using var verify = new CoreDbContext(db.Options);
        // Assert
        Assert.Null(await verify.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k2"));

        // SaveChanges 後に現れる
        await ctx.SaveChangesAsync();
        await using var verify2 = new CoreDbContext(db.Options);
        Assert.NotNull(await verify2.CommandDedup.FirstOrDefaultAsync(x => x.DedupKey == "k2"));
    }

    [Fact]
    public async Task FindValidConflictingRequestHashAsync_ReturnsRow_WhenSameIdempotencyKeyButDifferentHash()
    {
        using var db = new SqliteTestDatabase();
        var repo = new CommandDedupRepository(db.Factory);
        var now = DateTime.UtcNow;
        const string tenantId = "default";
        const string idem = "idem-e2e-test";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = $"{tenantId}|POST /v1/workflows:{idem}:AAA",
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = idem,
                RequestHash = "AAA",
                StatusCode = 201,
                ResponseBody = "{}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync();
        }

        var conflict = await repo.FindValidConflictingRequestHashAsync(
            tenantId,
            "POST /v1/workflows",
            idem,
            "BBB",
            now,
            default);

        Assert.NotNull(conflict);
        Assert.Equal("AAA", conflict!.RequestHash);
    }

    [Fact]
    public async Task FindValidConflictingRequestHashAsync_ReturnsNull_WhenHashMatches()
    {
        using var db = new SqliteTestDatabase();
        var repo = new CommandDedupRepository(db.Factory);
        var now = DateTime.UtcNow;
        const string tenantId = "default";
        const string hash = "SAME";

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.CommandDedup.Add(new CommandDedupRow
            {
                DedupKey = $"{tenantId}|POST /v1/workflows:key1:{hash}",
                Endpoint = "POST /v1/workflows",
                IdempotencyKey = "key1",
                RequestHash = hash,
                StatusCode = 201,
                ResponseBody = "{}",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });
            await ctx.SaveChangesAsync();
        }

        var conflict = await repo.FindValidConflictingRequestHashAsync(
            tenantId,
            "POST /v1/workflows",
            "key1",
            hash,
            now,
            default);

        Assert.Null(conflict);
    }
}

