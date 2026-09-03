using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence.Repositories;
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

    /// <summary>Worker 文脈（PrincipalId null）でも親 snapshot を継承できる。</summary>
    [Fact]
    public async Task CaptureForChildWorkflowAsync_WhenPrincipalIdNull_InheritsParentOwner()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerPrincipalId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "child-owner@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);

        var parentDefinitionId = Guid.NewGuid();
        var childDefinitionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext())
        {
            ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default", projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.DefaultTenantId,
                parentDefinitionId,
                "parent-def",
                projectId);
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.DefaultTenantId,
                childDefinitionId,
                "child-def",
                projectId);
            await seed.SaveChangesAsync();
        }

        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = ownerPrincipalId });
        var factory = CreateFactory(database);
        var capturedAt = DateTime.UtcNow;
        var parentSnapshot = await factory.CaptureForStartAsync(
            TestTenantIds.DefaultTenantId,
            parentDefinitionId,
            capturedAt,
            CancellationToken.None);

        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = null });

        // Act
        var childSnapshot = await factory.CaptureForChildWorkflowAsync(
            parentSnapshot,
            TestTenantIds.DefaultTenantId,
            childDefinitionId,
            parentDefinitionId,
            capturedAt.AddSeconds(1),
            CancellationToken.None);

        // Assert
        Assert.Equal(ownerPrincipalId, childSnapshot.StartedByPrincipalId);
        Assert.Equal(parentSnapshot.EffectivePermissionKeys, childSnapshot.EffectivePermissionKeys);
        Assert.Equal(parentSnapshot.PermissionSetHash, childSnapshot.PermissionSetHash);
        Assert.Equal(projectId, childSnapshot.AuthorizationContext.ProjectId);
        Assert.Contains(parentDefinitionId, childSnapshot.AncestorDefinitionIds);
        Assert.DoesNotContain(childDefinitionId, childSnapshot.AncestorDefinitionIds);
    }

    /// <summary>子定義が無いときは NotFound。</summary>
    [Fact]
    public async Task CaptureForChildWorkflowAsync_WhenChildDefinitionMissing_ThrowsNotFound()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var parentSnapshot = CreateMinimalParentSnapshot(TestTenantIds.DefaultTenantId);
        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = null });
        var factory = CreateFactory(database);

        // Act
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            factory.CaptureForChildWorkflowAsync(
                parentSnapshot,
                TestTenantIds.DefaultTenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow,
                CancellationToken.None));

        // Assert
        Assert.Equal(ExecutionValidationMessages.DefinitionNotFound, ex.Message);
    }

    /// <summary>親 snapshot のテナント不一致は失敗する。</summary>
    [Fact]
    public async Task CaptureForChildWorkflowAsync_WhenTenantMismatch_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var parentSnapshot = CreateMinimalParentSnapshot(Guid.NewGuid());
        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = null });
        var factory = CreateFactory(database);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CaptureForChildWorkflowAsync(
                parentSnapshot,
                TestTenantIds.DefaultTenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow,
                CancellationToken.None));
    }

    private static ExecutionSecuritySnapshot CreateMinimalParentSnapshot(Guid tenantId)
    {
        var keys = new[] { WellKnownPermissionKeys.ExecutionsWrite };
        return new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = Guid.NewGuid(),
            PrincipalType = "User",
            EffectivePermissionKeys = keys,
            PermissionSetHash = PermissionSetHash.Compute(keys),
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = Guid.NewGuid(),
                ProjectRole = "admin",
                GroupSnapshots = [],
                IsTenantAdmin = false
            },
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = DateTime.UtcNow
        };
    }

    private static ExecutionSecuritySnapshotFactory CreateFactory(SqliteTestDatabase database)
    {
        var uowFactory = new TestCoreUnitOfWorkFactory(database.Factory);
        return new ExecutionSecuritySnapshotFactory(
            database.TenantAccessor,
            new PrincipalDataAccessAdapter(new PlatformDataAccess(database.Factory, new DefaultIdGenerator())),
            new TestCoreTransactionExecutor(uowFactory),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new ProjectRepository(new DefaultIdGenerator()));
    }
}
