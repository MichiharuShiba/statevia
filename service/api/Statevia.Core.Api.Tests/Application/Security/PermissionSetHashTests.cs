using Statevia.Core.Api.Application.Security;

namespace Statevia.Core.Api.Tests.Application.Security;

/// <summary><see cref="PermissionSetHash"/> の検証。</summary>
public sealed class PermissionSetHashTests
{
    /// <summary>空集合は空入力の SHA-256 になる。</summary>
    [Fact]
    public void Compute_EmptyKeys_ReturnsEmptyPayloadHash()
    {
        // Act
        var actual = PermissionSetHash.Compute(Array.Empty<string>());
        var again = PermissionSetHash.Compute([]);

        // Assert
        Assert.Equal(again, actual);
        Assert.Matches("^[0-9a-f]{64}$", actual);
    }

    /// <summary>キーは ordinal 昇順で連結される。</summary>
    [Fact]
    public void Compute_UnorderedKeys_MatchesSortedNewlineJoinedHash()
    {
        // Act
        var actual = PermissionSetHash.Compute(["executions.write", "definitions.read"]);
        var sorted = PermissionSetHash.Compute(["definitions.read", "executions.write"]);

        // Assert
        Assert.Equal(sorted, actual);
    }

    /// <summary>重複キーは除去される。</summary>
    [Fact]
    public void Compute_DuplicateKeys_ProducesSameHashAsDistinct()
    {
        // Arrange
        var single = new[] { "executions.read" };

        // Act
        var fromDuplicates = PermissionSetHash.Compute(["executions.read", "executions.read"]);
        var fromSingle = PermissionSetHash.Compute(single);

        // Assert
        Assert.Equal(fromSingle, fromDuplicates);
    }
}
