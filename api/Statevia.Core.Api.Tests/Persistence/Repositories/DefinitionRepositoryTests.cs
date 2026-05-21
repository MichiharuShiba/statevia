using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence.Repositories;

public sealed class DefinitionRepositoryTests
{
    /// <summary>
    /// 未存在の識別子では空値を返す。
    /// </summary>
    [Fact]
    public async Task GetLatestByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var res = await repo.GetLatestByIdAsync(uow, "t1", Guid.NewGuid(), default);

        // Assert
        Assert.Null(res);
    }

    /// <summary>
    /// 初版追加後に最新版を取得できる。
    /// </summary>
    [Fact]
    public async Task AddWithInitialVersionAsync_PersistsRows_ThenGetLatestByIdAsyncReturns()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.AddWithInitialVersionAsync(
            uow,
            new DefinitionRow
            {
                DefinitionId = defId,
                TenantId = tenantId,
                Slug = DefinitionSlug.FromName(defId, "def-1"),
                Name = "def-1",
                LatestVersion = 1,
                CreatedAt = created,
                UpdatedAt = created
            },
            new DefinitionVersionRow
            {
                DefinitionVersionId = versionId,
                DefinitionId = defId,
                Version = 1,
                SourceYaml = "workflow:\n  name: x",
                CompiledJson = "{}",
                CreatedAt = created
            },
            default);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var readUow = await uowFactory.CreateAsync();
        var res = await repo.GetLatestByIdAsync(readUow, tenantId, defId, default);
        Assert.NotNull(res);
        Assert.Equal(defId, res!.Definition.DefinitionId);
        Assert.Equal("def-1", res.Definition.Name);
        Assert.Equal(versionId, res.Version.DefinitionVersionId);
    }

    /// <summary>
    /// publish は新版 INSERT 後に latest_version 投影を更新する。
    /// </summary>
    [Fact]
    public async Task PublishVersionAsync_AppendsVersionAndUpdatesLatest()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var seedUow = await uowFactory.CreateAsync())
        {
            await repo.AddWithInitialVersionAsync(
                seedUow,
                new DefinitionRow
                {
                    DefinitionId = defId,
                    TenantId = tenantId,
                    Slug = DefinitionSlug.FromName(defId, "def-1"),
                    Name = "def-1",
                    LatestVersion = 1,
                    CreatedAt = created,
                    UpdatedAt = created
                },
                new DefinitionVersionRow
                {
                    DefinitionVersionId = Guid.NewGuid(),
                    DefinitionId = defId,
                    Version = 1,
                    SourceYaml = "old",
                    CompiledJson = "{\"old\":true}",
                    CreatedAt = created
                },
                default);
            await seedUow.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        var newVersionId = Guid.NewGuid();
        await using var uow = await uowFactory.CreateAsync();
        var published = await repo.PublishVersionAsync(
            uow,
            new DefinitionVersionPublishCommand(tenantId, defId, "def-2", "new", "{\"new\":true}", newVersionId),
            default);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(published);
        Assert.Equal(2, published!.Definition.LatestVersion);
        Assert.Equal("def-2", published.Definition.Name);
        Assert.Equal(2, published.Version.Version);
        Assert.Equal("{\"new\":true}", published.Version.CompiledJson);

        await using var verify = await uowFactory.CreateAsync();
        var v1 = await repo.GetVersionAsync(verify, tenantId, defId, 1, default);
        Assert.NotNull(v1);
        Assert.Equal("{\"old\":true}", v1!.CompiledJson);
    }

    /// <summary>
    /// 他テナントの版は取得できない。
    /// </summary>
    [Fact]
    public async Task GetVersionByIdAsync_ReturnsNull_ForOtherTenant()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();
        var defId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using (var ctx = new CoreDbContext(db.Options))
        {
            DefinitionTestData.AddDefinitionWithVersion(ctx, "t-owner", defId, "def", versionId: versionId);
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var res = await repo.GetVersionByIdAsync(uow, "t-other", versionId, default);

        // Assert
        Assert.Null(res);
    }

    /// <summary>
    /// 表示用識別子がない定義も一覧に含める。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_UsesLeftJoin_ForMissingDisplayId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();
        var tenantId = "t1";
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();
        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, defId1, "A", createdAt: created1);
            DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, defId2, "B", createdAt: created2);
            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "definition",
                DisplayId = "DISP-A",
                ResourceId = defId1,
                CreatedAt = created1
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (_, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new DefinitionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery("createdAt", "asc"),
                NameContains: null),
            default);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("DISP-A", items[0].DisplayId);
        Assert.Null(items[1].DisplayId);
    }

    /// <summary>
    /// 名称部分一致で絞り込みつつページ取得する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByNameContains_AndPaginates()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = new DefinitionRepository();
        var tenantId = "t1";
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();
        var defId3 = Guid.NewGuid();
        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var created3 = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, defId1, "order-flow", createdAt: created1);
            DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, defId2, "payment-flow", createdAt: created2);
            DefinitionTestData.AddDefinitionWithVersion(ctx, tenantId, defId3, "order-detail", createdAt: created3);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            tenantId,
            new DefinitionListPageQuery(
                Page: new PageQuery(0, 1),
                Sort: new SortQuery(null, null),
                NameContains: "order"),
            default);

        // Assert
        Assert.Equal(2, total);
        Assert.Single(items);
        Assert.Equal(defId1, items[0].Detail.Definition.DefinitionId);
    }
}
