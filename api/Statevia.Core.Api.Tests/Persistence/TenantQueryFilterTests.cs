using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence;

/// <summary>HasQueryFilter の fail-closed 挙動。</summary>
public sealed class TenantQueryFilterTests
{
    /// <summary>テナント未設定時は tenant スコープ行が返らない。</summary>
    [Fact]
    public async Task UnresolvedTenantContext_ReturnsEmpty_ForTenantScopedEntities()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var enabledOptions = database.Options;
        var accessor = database.TenantAccessor;
        accessor.Set(null);

        var definitionId = Guid.NewGuid();
        await using (var seed = new CoreDbContext(enabledOptions, accessor, DisabledTenantQueryFilterOptions.Instance))
        {
            seed.Definitions.Add(new DefinitionRow
            {
                DefinitionId = definitionId,
                TenantId = "default",
                Slug = "fail-closed-test",
                Name = "test",
                LatestVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var db = new CoreDbContext(enabledOptions, accessor, EnabledTenantQueryFilterOptions.Instance);
        var rows = await db.Definitions.AsNoTracking().ToListAsync();

        // Assert
        Assert.Empty(rows);
    }

    /// <summary>テナント解決後は当該 tenant_key の行のみ返る。</summary>
    [Fact]
    public async Task ResolvedTenantContext_ReturnsOnlyMatchingTenantKey()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = database.TenantAccessor;
        var options = database.Options;
        var otherTenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var seed = new CoreDbContext(options, accessor, DisabledTenantQueryFilterOptions.Instance))
        {
            accessor.Set(new TenantContextState(
                otherTenantId, "other", null, TenantLifecycle.Active));
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();

            accessor.Set(null);
            seed.Definitions.Add(new DefinitionRow
            {
                DefinitionId = Guid.NewGuid(),
                TenantId = "default",
                Slug = "default-slug",
                Name = "default",
                LatestVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            seed.Definitions.Add(new DefinitionRow
            {
                DefinitionId = Guid.NewGuid(),
                TenantId = "other",
                Slug = "other-slug",
                Name = "other",
                LatestVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        accessor.Set(TestTenantIds.DefaultContext);

        // Act
        await using var db = new CoreDbContext(options, accessor, EnabledTenantQueryFilterOptions.Instance);
        var rows = await db.Definitions.AsNoTracking().ToListAsync();

        // Assert
        Assert.Single(rows);
        Assert.Equal("default", rows[0].TenantId);
    }
}
