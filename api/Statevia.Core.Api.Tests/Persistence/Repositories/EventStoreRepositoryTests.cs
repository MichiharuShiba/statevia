using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class EventStoreRepositoryTests
{
    private static SqliteTestDatabase CreateDb() => new();

    /// <summary>
    /// イベント追加時に連番が1から順に採番されることを確認する。
    /// </summary>
    [Fact]
    public async Task AppendAsync_AssignsIncrementingSeq_From1()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowStarted, payloadJson: null, default);
        await repo.AppendAsync(wfId, EventStoreEventType.EventPublished, payloadJson: "{}", default);

        await using var ctx = new CoreDbContext(db.Options);
        var seqs = await ctx.EventStore.AsNoTracking()
            .Where(e => e.WorkflowId == wfId)
            .OrderBy(e => e.Seq)
            .Select(e => e.Seq)
            .ToListAsync();

        // Assert
        Assert.Equal(new long[] { 1, 2 }, seqs);
    }

    /// <summary>
    /// 既存文脈に追加したイベントは保存前に永続化されない。
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithDb_AddsWithoutSaving()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await using var ctx = new CoreDbContext(db.Options);
        await repo.AppendAsync(ctx, wfId, EventStoreEventType.WorkflowStarted, payloadJson: null, default);

        // SaveChanges 前なので未永続化
        // Assert
        Assert.Empty(await ctx.EventStore.AsNoTracking().Where(e => e.WorkflowId == wfId).ToListAsync());

        await ctx.SaveChangesAsync();

        Assert.Equal(1, await ctx.EventStore.AsNoTracking().CountAsync(e => e.WorkflowId == wfId));
    }

    /// <summary>
    /// 取得件数上限を超えると追加データありを示す。
    /// </summary>
    [Fact]
    public async Task ListAfterSeqAsync_HasMore_WhenMoreThanLimit()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowStarted, null, default);
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowCancelled, null, default);
        await repo.AppendAsync(wfId, EventStoreEventType.EventPublished, null, default);

        // Assert
        var (items, hasMore) = await repo.ListAfterSeqAsync(wfId, afterSeq: 0, limit: 2, default);
        Assert.True(hasMore);
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Seq);
        Assert.Equal(2, items[1].Seq);
    }

    /// <summary>
    /// 取得件数が上限内なら追加データなしを示す。
    /// </summary>
    [Fact]
    public async Task ListAfterSeqAsync_NoMore_WhenWithinLimit()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowStarted, null, default);
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowCancelled, null, default);

        // Assert
        var (items, hasMore) = await repo.ListAfterSeqAsync(wfId, afterSeq: 0, limit: 5, default);
        Assert.False(hasMore);
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Seq);
        Assert.Equal(2, items[1].Seq);
    }

    /// <summary>
    /// 不正なafterSeqとlimitを指定すると引数例外を送出する。
    /// </summary>
    [Fact]
    public async Task ListAfterSeqAsync_ValidatesArguments()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ListAfterSeqAsync(wfId, afterSeq: -1, limit: 1, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ListAfterSeqAsync(wfId, afterSeq: 0, limit: 0, default));
    }

    /// <summary>
    /// イベントがないとき最大連番は零を返す。
    /// </summary>
    [Fact]
    public async Task GetMaxSeqAsync_Returns0_WhenEmpty()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        var max = await repo.GetMaxSeqAsync(wfId, default);
        // Assert
        Assert.Equal(0, max);
    }

    /// <summary>
    /// イベントがあるとき最大連番を返す。
    /// </summary>
    [Fact]
    public async Task GetMaxSeqAsync_ReturnsMax_WhenNotEmpty()
    {
        // Arrange
        using var db = CreateDb();
        var repo = new EventStoreRepository(db.Factory, new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowStarted, null, default);
        await repo.AppendAsync(wfId, EventStoreEventType.WorkflowCancelled, null, default);

        // Assert
        var max = await repo.GetMaxSeqAsync(wfId, default);
        Assert.Equal(2, max);
    }
}

