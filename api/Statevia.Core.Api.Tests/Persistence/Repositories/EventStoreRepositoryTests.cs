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
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await using (var uow = await uowFactory.CreateAsync())
        {
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowStarted, payloadJson: null, default);
            await repo.AppendAsync(uow, wfId, EventStoreEventType.EventPublished, payloadJson: "{}", default);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        await using var ctx = new CoreDbContext(db.Options);
        var seqs = await ctx.EventStore.AsNoTracking()
            .Where(e => e.ExecutionId == wfId)
            .OrderBy(e => e.Seq)
            .Select(e => e.Seq)
            .ToListAsync();

        // Assert
        Assert.Equal(new long[] { 1, 2 }, seqs);
    }

    /// <summary>
    /// 同一 UoW に追加したイベントは SaveChanges 前に永続化されない。
    /// </summary>
    [Fact]
    public async Task AppendAsync_WithDb_AddsWithoutSaving()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowStarted, payloadJson: null, default);

        // Assert
        Assert.Empty(await uow.Db.EventStore.AsNoTracking().Where(e => e.ExecutionId == wfId).ToListAsync());

        await uow.SaveChangesAsync(CancellationToken.None);
        Assert.Equal(1, await uow.Db.EventStore.AsNoTracking().CountAsync(e => e.ExecutionId == wfId));
    }

    /// <summary>
    /// 取得件数上限を超えると追加データありを示す。
    /// </summary>
    [Fact]
    public async Task ListAfterSeqAsync_HasMore_WhenMoreThanLimit()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();

        await using (var uow = await uowFactory.CreateAsync())
        {
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowStarted, null, default);
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowCancelled, null, default);
            await repo.AppendAsync(uow, wfId, EventStoreEventType.EventPublished, null, default);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var readUow = await uowFactory.CreateAsync();
        var (items, hasMore) = await repo.ListAfterSeqAsync(readUow, wfId, afterSeq: 0, limit: 2, default);

        // Assert
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
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();

        await using (var uow = await uowFactory.CreateAsync())
        {
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowStarted, null, default);
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowCancelled, null, default);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var readUow = await uowFactory.CreateAsync();
        var (items, hasMore) = await repo.ListAfterSeqAsync(readUow, wfId, afterSeq: 0, limit: 5, default);

        // Assert
        Assert.False(hasMore);
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Seq);
        Assert.Equal(2, items[1].Seq);
    }

    /// <summary>
    /// 不正な afterSeq と limit を指定すると引数例外を送出する。
    /// </summary>
    [Fact]
    public async Task ListAfterSeqAsync_ValidatesArguments()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();
        await using var uow = await uowFactory.CreateAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repo.ListAfterSeqAsync(uow, wfId, afterSeq: -1, limit: 1, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repo.ListAfterSeqAsync(uow, wfId, afterSeq: 0, limit: 0, default));
    }

    /// <summary>
    /// イベントがないとき最大連番は零を返す。
    /// </summary>
    [Fact]
    public async Task GetMaxSeqAsync_Returns0_WhenEmpty()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();
        await using var uow = await uowFactory.CreateAsync();

        // Act
        var max = await repo.GetMaxSeqAsync(uow, wfId, default);

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
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var wfId = Guid.NewGuid();

        await using (var uow = await uowFactory.CreateAsync())
        {
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowStarted, null, default);
            await repo.AppendAsync(uow, wfId, EventStoreEventType.WorkflowCancelled, null, default);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var readUow = await uowFactory.CreateAsync();
        var max = await repo.GetMaxSeqAsync(readUow, wfId, default);

        // Assert
        Assert.Equal(2, max);
    }

    /// <summary>
    /// 同一 UoW で同一 client 冪等追記を二度呼ぶと二回目は false となり 1 行のみ保存される。
    /// </summary>
    [Fact]
    public async Task TryAppendIfAbsentByClientEventAsync_SecondCallInSameContext_ReturnsFalse()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        await using var uow = await uowFactory.CreateAsync();

        // Act
        var first = await repo.TryAppendIfAbsentByClientEventAsync(
            uow,
            workflowId,
            clientEventId,
            EventStoreEventType.EventPublished,
            payloadJson: "{\"x\":1}",
            default);
        var second = await repo.TryAppendIfAbsentByClientEventAsync(
            uow,
            workflowId,
            clientEventId,
            EventStoreEventType.EventPublished,
            payloadJson: "{\"x\":1}",
            default);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await uow.Db.EventStore.AsNoTracking().CountAsync(e => e.ExecutionId == workflowId));
    }

    /// <summary>
    /// 保存後に新しい UoW で同一 client 冪等追記を呼ぶと false となる。
    /// </summary>
    [Fact]
    public async Task TryAppendIfAbsentByClientEventAsync_AfterPersist_SecondContext_ReturnsFalse()
    {
        // Arrange
        using var db = CreateDb();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new EventStoreRepository(new UuidV7Generator());
        var workflowId = Guid.NewGuid();
        var clientEventId = Guid.Parse("7ba7b810-9dad-11d1-80b4-00c04fd430c8");

        await using (var uow1 = await uowFactory.CreateAsync())
        {
            Assert.True(await repo.TryAppendIfAbsentByClientEventAsync(
                uow1,
                workflowId,
                clientEventId,
                EventStoreEventType.WorkflowCancelled,
                "{}",
                default));
            await uow1.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow2 = await uowFactory.CreateAsync();
        var again = await repo.TryAppendIfAbsentByClientEventAsync(
            uow2,
            workflowId,
            clientEventId,
            EventStoreEventType.WorkflowCancelled,
            "{}",
            default);

        // Assert
        Assert.False(again);
        Assert.Equal(1, await uow2.Db.EventStore.AsNoTracking().CountAsync(e => e.ExecutionId == workflowId));
    }
}
