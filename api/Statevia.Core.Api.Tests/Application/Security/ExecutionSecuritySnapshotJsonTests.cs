using Statevia.Core.Api.Application.Security;

namespace Statevia.Core.Api.Tests.Application.Security;

/// <summary><see cref="ExecutionSecuritySnapshotJson"/> の検証。</summary>
public sealed class ExecutionSecuritySnapshotJsonTests
{
    /// <summary>シリアル化した JSON を復元できる。</summary>
    [Fact]
    public void TryDeserialize_ValidJson_RoundTrips()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var keys = new[] { WellKnownPermissionKeys.ExecutionsWrite };
        var snapshot = new ExecutionSecuritySnapshot
        {
            TenantId = Guid.NewGuid(),
            StartedByPrincipalId = ownerId,
            PrincipalType = "User",
            EffectivePermissionKeys = keys,
            PermissionSetHash = PermissionSetHash.Compute(keys),
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = Guid.NewGuid(),
                ProjectRole = "executor",
                GroupSnapshots = [new GroupSnapshot(Guid.NewGuid(), "Operators")],
                IsTenantAdmin = false
            },
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = DateTime.UtcNow
        };
        var json = ExecutionSecuritySnapshotJson.Serialize(snapshot);

        // Act
        var restored = ExecutionSecuritySnapshotJson.TryDeserialize(json);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(snapshot.StartedByPrincipalId, restored.StartedByPrincipalId);
        Assert.Equal(snapshot.PermissionSetHash, restored.PermissionSetHash);
        Assert.Equal(snapshot.EvaluationMode, restored.EvaluationMode);
        Assert.Equal("Operators", restored.AuthorizationContext.GroupSnapshots[0].Name);
    }

    /// <summary>空文字列は null を返す。</summary>
    [Fact]
    public void TryDeserialize_NullOrWhitespace_ReturnsNull()
    {
        // Act
        var fromNull = ExecutionSecuritySnapshotJson.TryDeserialize(null);
        var fromWhitespace = ExecutionSecuritySnapshotJson.TryDeserialize("   ");

        // Assert
        Assert.Null(fromNull);
        Assert.Null(fromWhitespace);
    }
}
