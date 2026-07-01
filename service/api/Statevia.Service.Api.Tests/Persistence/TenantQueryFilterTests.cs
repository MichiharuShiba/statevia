using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence;

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

        var executionId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using (var seed = new CoreDbContext(enabledOptions, accessor, DisabledTenantQueryFilterOptions.Instance))
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, TestTenantIds.DefaultTenantId,
                definitionId,
                "wf-filter",
                projectId,
                versionId: versionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = executionId,
                TenantId = TestTenantIds.DefaultTenantId,
                DefinitionId = definitionId,
                DefinitionVersionId = versionId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CancelRequested = false,
                RestartLost = false
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var db = new CoreDbContext(enabledOptions, accessor, EnabledTenantQueryFilterOptions.Instance);
        var rows = await db.Executions.AsNoTracking().ToListAsync();

        // Assert
        Assert.Empty(rows);
    }

    /// <summary>テナント解決後は当該 tenant 内部 ID の行のみ返る。</summary>
    [Fact]
    public async Task ResolvedTenantContext_ReturnsOnlyMatchingTenantId()
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
            var defaultProjectId = Guid.NewGuid();
            var otherProjectId = Guid.NewGuid();
            var defaultDefinitionId = Guid.NewGuid();
            var otherDefinitionId = Guid.NewGuid();
            var defaultVersionId = Guid.NewGuid();
            var otherVersionId = Guid.NewGuid();
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", defaultProjectId);
            ProjectTestData.AddDefaultProject(seed, otherTenantId, "other", otherProjectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, TestTenantIds.DefaultTenantId, defaultDefinitionId, "default-def", defaultProjectId, versionId: defaultVersionId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed, otherTenantId, otherDefinitionId, "other-def", otherProjectId, versionId: otherVersionId);
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = Guid.NewGuid(),
                TenantId = TestTenantIds.DefaultTenantId,
                DefinitionId = defaultDefinitionId,
                DefinitionVersionId = defaultVersionId,
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
            seed.Executions.Add(new ExecutionRow
            {
                ExecutionId = Guid.NewGuid(),
                TenantId = otherTenantId,
                DefinitionId = otherDefinitionId,
                DefinitionVersionId = otherVersionId,
                Status = "Running",
                StartedAt = now,
                UpdatedAt = now,
                CancelRequested = false,
                RestartLost = false
            });
            await seed.SaveChangesAsync();
        }

        accessor.Set(TestTenantIds.DefaultContext);

        // Act
        await using var db = new CoreDbContext(options, accessor, EnabledTenantQueryFilterOptions.Instance);
        var rows = await db.Executions.AsNoTracking().ToListAsync();

        // Assert
        Assert.Single(rows);
        Assert.Equal(TestTenantIds.DefaultTenantId, rows[0].TenantId);
    }
}
