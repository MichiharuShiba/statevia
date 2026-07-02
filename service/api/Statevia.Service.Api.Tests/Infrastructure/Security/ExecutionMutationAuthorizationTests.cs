
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="ExecutionMutationAuthorization"/> の検証。</summary>
public sealed class ExecutionMutationAuthorizationTests
{
    /// <summary>Owner + Snapshot でスナップショット上に key があれば成功する。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OwnerSnapshotWithKey_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "owner@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);

        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsWrite]);
        var authorization = CreateAuthorization(database, ownerId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Owner + Snapshot で Live 権限が剥奪されてもスナップショット上に key があれば成功する。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OwnerSnapshotAfterLiveRevoke_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserAsync(
            database,
            "owner-no-live@example.com",
            "password",
            isTenantAdmin: false);

        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsWrite]);
        var authorization = CreateAuthorization(database, ownerId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Owner + Snapshot でスナップショットに key が無ければ Forbidden。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OwnerSnapshotWithoutKey_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "owner-missing@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);

        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsRead]);
        var authorization = CreateAuthorization(database, ownerId);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, ex.Code);
    }

    /// <summary>Operator は常に Live 評価で、Live に key が無ければ Forbidden。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OperatorWithoutLivePermission_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "owner2@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);
        var operatorId = await SecurityTestSeed.SeedUserAsync(
            database,
            "operator@example.com",
            "password",
            isTenantAdmin: false);

        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsWrite]);
        var authorization = CreateAuthorization(database, operatorId);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, ex.Code);
    }

    /// <summary>Operator は Live 権限があれば Owner の Snapshot に関係なく成功する。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OperatorWithLivePermission_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "owner3@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsRead]);
        var operatorId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "operator-ok@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);

        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsRead]);
        var authorization = CreateAuthorization(database, operatorId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>スナップショット未保存 execution は Live にフォールバックする。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_NullSnapshot_UsesLivePermission()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            database,
            "live-only@example.com",
            "password",
            [WellKnownPermissionKeys.ExecutionsWrite]);
        var authorization = CreateAuthorization(database, principalId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            authorization.EnsureMutationPermissionAsync(
                null,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Owner + Live は現在の permission を再評価する。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_OwnerLiveWithoutPermission_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserAsync(
            database,
            "owner-live@example.com",
            "password",
            isTenantAdmin: false);

        var snapshot = CreateSnapshot(
            ownerId,
            [WellKnownPermissionKeys.ExecutionsWrite],
            SecurityEvaluationMode.Live);
        var authorization = CreateAuthorization(database, ownerId);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Equal(RuntimePermissionAuthorization.PermissionDeniedCode, ex.Code);
    }

    /// <summary>無効 Principal は Identity で Forbidden。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_InactivePrincipal_ThrowsPrincipalInactive()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var ownerId = await SecurityTestSeed.SeedUserAsync(
            database,
            "inactive@example.com",
            "password",
            isActive: false,
            isTenantAdmin: false);
        var snapshot = CreateSnapshot(ownerId, [WellKnownPermissionKeys.ExecutionsWrite]);
        var authorization = CreateAuthorization(database, ownerId);

        // Act
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            authorization.EnsureMutationPermissionAsync(
                snapshot,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Equal(PrincipalIdentityAuthorization.PrincipalInactiveCode, ex.Code);
    }

    /// <summary>permissionKey 未指定は ArgumentException。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_EmptyPermissionKey_ThrowsArgumentException()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(database, "key@example.com", "password");
        var authorization = CreateAuthorization(database, principalId);

        // Act
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            authorization.EnsureMutationPermissionAsync(null, "  ", CancellationToken.None));

        // Assert
        Assert.Equal("permissionKey", ex.ParamName);
    }

    /// <summary>Principal 未解決は Unauthorized。</summary>
    [Fact]
    public async Task EnsureMutationPermissionAsync_MissingPrincipal_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext);
        var authorization = new ExecutionMutationAuthorization(
            accessor,
            new RuntimePermissionAuthorization(accessor, new PlatformDataAccess(database.Factory)),
            new PlatformDataAccess(database.Factory));

        // Act
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authorization.EnsureMutationPermissionAsync(
                null,
                WellKnownPermissionKeys.ExecutionsWrite,
                CancellationToken.None));

        // Assert
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    private static ExecutionMutationAuthorization CreateAuthorization(SqliteTestDatabase database, Guid principalId)
    {
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        return new ExecutionMutationAuthorization(
            accessor,
            new RuntimePermissionAuthorization(accessor, new PlatformDataAccess(database.Factory)),
            new PlatformDataAccess(database.Factory));
    }

    private static ExecutionSecuritySnapshot CreateSnapshot(
        Guid ownerId,
        IReadOnlyList<string> keys,
        SecurityEvaluationMode evaluationMode = SecurityEvaluationMode.Snapshot) =>
        new()
        {
            TenantId = TestTenantIds.DefaultTenantId,
            StartedByPrincipalId = ownerId,
            PrincipalType = "User",
            EffectivePermissionKeys = keys,
            PermissionSetHash = PermissionSetHash.Compute(keys),
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = Guid.NewGuid(),
                ProjectRole = "executor",
                GroupSnapshots = Array.Empty<GroupSnapshot>(),
                IsTenantAdmin = false
            },
            EvaluationMode = evaluationMode,
            CapturedAt = DateTime.UtcNow
        };
}
