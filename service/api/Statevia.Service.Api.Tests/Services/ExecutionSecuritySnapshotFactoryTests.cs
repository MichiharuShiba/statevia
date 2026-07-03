
using Statevia.Service.Api.Contracts;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionSecuritySnapshotFactory"/> の検証。</summary>
public sealed class ExecutionSecuritySnapshotFactoryTests
{
    /// <summary>Start 用スナップショットを構築できる。</summary>
    [Fact]
    public async Task CaptureForStartAsync_ValidPrincipal_CapturesSnapshot()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "snap@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);

        var definitionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.DefaultTenantId,
                definitionId,
                "snap-def",
                projectId);
            await seed.SaveChangesAsync();
        }

        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var factory = CreateFactory(database);

        // Act
        var capturedAt = DateTime.UtcNow;
        var snapshot = await factory.CaptureForStartAsync(
            TestTenantIds.DefaultTenantId,
            definitionId,
            capturedAt,
            CancellationToken.None);

        // Assert
        Assert.Equal(principalId, snapshot.StartedByPrincipalId);
        Assert.Equal("User", snapshot.PrincipalType);
        Assert.Equal(SecurityEvaluationMode.Snapshot, snapshot.EvaluationMode);
        Assert.Contains(WellKnownPermissionKeys.ExecutionsWrite, snapshot.EffectivePermissionKeys);
        Assert.Equal(capturedAt, snapshot.CapturedAt);
        Assert.Equal(projectId, snapshot.AuthorizationContext.ProjectId);
        Assert.Equal("admin", snapshot.AuthorizationContext.ProjectRole);
        Assert.NotEmpty(snapshot.PermissionSetHash);
    }

    /// <summary>API キー経路では交差済み scopes をスナップショットに固定する。</summary>
    [Fact]
    public async Task CaptureForStartAsync_ApiKeyEffectiveScopes_UsesFixedSet()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (principalId, _, _) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var definitionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.DefaultTenantId,
                definitionId,
                "api-def",
                projectId);
            await seed.SaveChangesAsync();
        }

        var effectiveScopes = new HashSet<string>(StringComparer.Ordinal)
        {
            WellKnownPermissionKeys.ExecutionsRead
        };
        database.TenantAccessor.Set(TestTenantIds.DefaultContext with
        {
            PrincipalId = principalId,
            EffectivePermissionKeys = effectiveScopes
        });
        var factory = CreateFactory(database);

        // Act
        var snapshot = await factory.CaptureForStartAsync(
            TestTenantIds.DefaultTenantId,
            definitionId,
            DateTime.UtcNow,
            CancellationToken.None);

        // Assert
        Assert.Equal("ServiceAccount", snapshot.PrincipalType);
        Assert.Single(snapshot.EffectivePermissionKeys);
        Assert.Equal(WellKnownPermissionKeys.ExecutionsRead, snapshot.EffectivePermissionKeys[0]);
        Assert.NotEmpty(snapshot.AuthorizationContext.GroupSnapshots);
    }

    /// <summary>Principal 未解決は Unauthorized。</summary>
    [Fact]
    public async Task CaptureForStartAsync_MissingPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        database.TenantAccessor.Set(TestTenantIds.DefaultContext);
        var factory = CreateFactory(database);

        // Act
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            factory.CaptureForStartAsync(
                TestTenantIds.DefaultTenantId,
                Guid.NewGuid(),
                DateTime.UtcNow,
                CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    private static ExecutionSecuritySnapshotFactory CreateFactory(SqliteTestDatabase database)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(database.Factory);
        return new ExecutionSecuritySnapshotFactory(
            database.TenantAccessor,
            new PlatformDataAccess(database.Factory),
            new TestCoreTransactionExecutor(uowFactory),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new ProjectRepository());
    }
}
