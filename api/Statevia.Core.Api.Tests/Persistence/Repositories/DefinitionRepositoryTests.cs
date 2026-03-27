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
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new DefinitionRepository(db.Factory);

        // Act
        var res = await repo.GetByIdAsync("t1", Guid.NewGuid(), default);
        // Assert
        Assert.Null(res);
    }

    /// <summary>
    /// 行を永続化する の挙動を確認する。
    /// </summary>
    [Fact]
    public async Task AddAsync_PersistsRow_ThenGetByIdAsyncReturns()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new DefinitionRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId = Guid.NewGuid();
        await repo.AddAsync(new WorkflowDefinitionRow
        {
            DefinitionId = defId,
            TenantId = tenantId,
            Name = "def-1",
            SourceYaml = "workflow:\n  name: x",
            CompiledJson = "{}",
            CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, default);

        // Assert
        var res = await repo.GetByIdAsync(tenantId, defId, default);
        Assert.NotNull(res);
        Assert.Equal(defId, res!.DefinitionId);
        Assert.Equal("def-1", res.Name);
    }

    /// <summary>
    /// 表示用識別子がない定義も一覧に含める。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsAsync_UsesLeftJoin_ForMissingDisplayId()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new DefinitionRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();

        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.AddRange(
                new WorkflowDefinitionRow
                {
                    DefinitionId = defId1,
                    TenantId = tenantId,
                    Name = "A",
                    SourceYaml = "x",
                    CompiledJson = "{}",
                    CreatedAt = created1
                },
                new WorkflowDefinitionRow
                {
                    DefinitionId = defId2,
                    TenantId = tenantId,
                    Name = "B",
                    SourceYaml = "x",
                    CompiledJson = "{}",
                    CreatedAt = created2
                });

            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "definition",
                DisplayId = "DISP-A",
                ResourceId = defId1,
                CreatedAt = created1
            });

            await ctx.SaveChangesAsync();
        }

        var list = await repo.ListWithDisplayIdsAsync(tenantId, default);

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Equal("DISP-A", list[0].DisplayId);
        Assert.Null(list[1].DisplayId);
    }

    /// <summary>
    /// 名称部分一致で絞り込みつつページ取得する。
    /// </summary>
    [Fact]
    public async Task ListWithDisplayIdsPageAsync_FiltersByNameContains_AndPaginates()
    {
        // Arrange
        using var db = new InMemoryTestDatabase();
        var repo = new DefinitionRepository(db.Factory);

        // Act
        var tenantId = "t1";
        var defId1 = Guid.NewGuid();
        var defId2 = Guid.NewGuid();
        var defId3 = Guid.NewGuid();

        var created1 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created2 = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var created3 = new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = new CoreDbContext(db.Options))
        {
            ctx.WorkflowDefinitions.AddRange(
                new WorkflowDefinitionRow
                {
                    DefinitionId = defId1,
                    TenantId = tenantId,
                    Name = "order-flow",
                    SourceYaml = "x",
                    CompiledJson = "{}",
                    CreatedAt = created1
                },
                new WorkflowDefinitionRow
                {
                    DefinitionId = defId2,
                    TenantId = tenantId,
                    Name = "payment-flow",
                    SourceYaml = "x",
                    CompiledJson = "{}",
                    CreatedAt = created2
                },
                new WorkflowDefinitionRow
                {
                    DefinitionId = defId3,
                    TenantId = tenantId,
                    Name = "order-detail",
                    SourceYaml = "x",
                    CompiledJson = "{}",
                    CreatedAt = created3
                });
            await ctx.SaveChangesAsync();
        }

        var (total, items) = await repo.ListWithDisplayIdsPageAsync(
            tenantId,
            offset: 0,
            limit: 1,
            nameContains: "order",
            default);

        // Assert
        Assert.Equal(2, total);
        Assert.Single(items);
        Assert.Equal(defId1, items[0].Def.DefinitionId);
    }
}

