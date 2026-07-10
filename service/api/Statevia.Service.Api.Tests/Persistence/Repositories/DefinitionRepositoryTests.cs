
using Statevia.Service.Api.Contracts;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

public sealed class DefinitionRepositoryTests
{
    private static readonly Guid OwnerTenantId = TestTenantIds.DefaultTenantId;
    private const string OwnerTenantKey = "default";

    /// <summary>
    /// 未存在の識別子では空値を返す。
    /// </summary>
    [Fact]
    public async Task GetLatestForApiAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var res = await repo.GetLatestForApiAsync(uow, OwnerTenantId, Guid.NewGuid(), default);

        // Assert
        Assert.Null(res);
    }

    /// <summary>
    /// 初版追加後に最新版を取得できる。
    /// </summary>
    [Fact]
    public async Task AddWithInitialVersionAsync_PersistsRows_ThenGetLatestForApiAsyncReturns()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, OwnerTenantId, OwnerTenantKey, projectId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        await repo.AddWithInitialVersionAsync(
            uow,
            new DefinitionRow
            {
                DefinitionId = defId,
                TenantId = OwnerTenantId,
                ProjectId = projectId,
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
        var res = await repo.GetLatestForApiAsync(readUow, OwnerTenantId, defId, default);
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
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, OwnerTenantId, defId, "def-1", projectId, createdAt: created);
            await seed.SaveChangesAsync();
        }

        // Act
        var newVersionId = Guid.NewGuid();
        await using var uow = await uowFactory.CreateAsync();
        var published = await repo.PublishVersionAsync(
            uow,
            new DefinitionVersionPublishCommand(OwnerTenantId, defId, "def-2", "new", "{\"new\":true}", newVersionId),
            default);
        await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(published);
        Assert.Equal(2, published!.Definition.LatestVersion);
        Assert.Equal("def-2", published.Definition.Name);
        Assert.Equal(2, published.Version.Version);
        Assert.Equal("{\"new\":true}", published.Version.CompiledJson);

        await using var verify = await uowFactory.CreateAsync();
        var v1 = await repo.GetVersionForExecutionAsync(verify, OwnerTenantId, defId, 1, default);
        Assert.NotNull(v1);
        Assert.Equal("{}", v1!.CompiledJson);
    }

    /// <summary>
    /// 他テナントの版は取得できない。
    /// </summary>
    [Fact]
    public async Task GetVersionForExecutionByIdAsync_ReturnsNull_ForOtherTenant()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var ownerTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var ctx = db.Factory.CreateDbContext())
        {
            ctx.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "t-owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            ctx.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "t-other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            ProjectTestData.AddDefaultProject(ctx, ownerTenantId, "t-owner", projectId);
            DefinitionTestData.AddDefinitionWithVersion(ctx, ownerTenantId, defId, "def", projectId, versionId: versionId);
            await ctx.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            repo.GetVersionForExecutionByIdAsync(uow, otherTenantId, versionId, default));

        // Assert
        Assert.NotNull(ex);
    }

    /// <summary>
    /// 表示用識別子がない定義も一覧に含める。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_UsesLeftJoin_ForMissingDisplayId()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();
        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();

        await using (var ctx = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(ctx, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(ctx, OwnerTenantId, defId1, "A", projectId, createdAt: created1);
            DefinitionTestData.AddDefinitionWithVersion(ctx, OwnerTenantId, defId2, "B", projectId, createdAt: created2);
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
            OwnerTenantId,
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
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();
        var defId3 = Guid.NewGuid();
        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var created3 = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();

        await using (var ctx = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(ctx, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(ctx, OwnerTenantId, defId1, "order-flow", projectId, createdAt: created1);
            DefinitionTestData.AddDefinitionWithVersion(ctx, OwnerTenantId, defId2, "payment-flow", projectId, createdAt: created2);
            DefinitionTestData.AddDefinitionWithVersion(ctx, OwnerTenantId, defId3, "order-detail", projectId, createdAt: created3);
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            OwnerTenantId,
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

    /// <summary>
    /// 削除済み定義は ForApi では取得できないが ForExecution では版を取得できる。
    /// </summary>
    [Fact]
    public async Task GetLatestForApiAsync_ExcludesDeleted_WhileGetVersionForExecutionAsync_IncludesVersion()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var deletedAt = new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed, OwnerTenantId, defId, "def-1", projectId, createdAt: created);
            var definition = await seed.Definitions.FindAsync(defId);
            definition!.DeletedAt = deletedAt;
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var apiDetail = await repo.GetLatestForApiAsync(uow, OwnerTenantId, defId, default);
        var executionVersion = await repo.GetVersionForExecutionAsync(uow, OwnerTenantId, defId, 1, default);

        // Assert
        Assert.Null(apiDetail);
        Assert.NotNull(executionVersion);
        Assert.Equal(1, executionVersion!.Version);
    }

    /// <summary>
    /// soft delete 後は一覧から除外し、includeDeleted で含める。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_ExcludesDeletedUnlessIncludeDeleted()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, OwnerTenantId, defId, "def-1", projectId, createdAt: created);
            var definition = await seed.Definitions.FindAsync(defId);
            definition!.DeletedAt = created.AddDays(1);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var (defaultTotal, _) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            OwnerTenantId,
            new DefinitionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                NameContains: null),
            default);
        var (includedTotal, includedItems) = await repo.ListWithDisplayIdsPageAsync(
            uow,
            OwnerTenantId,
            new DefinitionListPageQuery(
                Page: new PageQuery(0, 10),
                Sort: new SortQuery(null, null),
                NameContains: null,
                IncludeDeleted: true),
            default);

        // Assert
        Assert.Equal(0, defaultTotal);
        Assert.Equal(1, includedTotal);
        Assert.NotNull(includedItems[0].Detail.Definition.DeletedAt);
    }

    /// <summary>
    /// 同一 UoW 内で restore した直後は、追跡エンティティから詳細を返せる（SaveChanges 前の GetLatestForApi は null）。
    /// </summary>
    [Fact]
    public async Task RestoreAsync_ReturnsDetail_BeforeSaveChanges_WhileGetLatestForApiStillNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
        var defId = Guid.NewGuid();
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var deletedAt = new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var seed = db.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, OwnerTenantId, OwnerTenantKey, projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed, OwnerTenantId, defId, "def-1", projectId, createdAt: created);
            var definition = await seed.Definitions.FindAsync(defId);
            definition!.DeletedAt = deletedAt;
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var restored = await repo.RestoreAsync(uow, OwnerTenantId, defId, default);
        var apiBeforeSave = await repo.GetLatestForApiAsync(uow, OwnerTenantId, defId, default);

        // Assert — restore は追跡行から詳細を返す。AsNoTracking+activeOnly はまだ DB 上 deleted。
        Assert.NotNull(restored);
        Assert.Null(restored!.Definition.DeletedAt);
        Assert.Equal(defId, restored.Definition.DefinitionId);
        Assert.NotNull(restored.Version);
        Assert.Null(apiBeforeSave);
    }
}
