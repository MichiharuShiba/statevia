using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Tests.Abstractions.Services;

/// <summary><see cref="CommandDedupKey"/> の等値・ハッシュのテスト。</summary>
public sealed class CommandDedupKeyTests
{
    /// <summary>同一フィールドのキーは等しい。</summary>
    [Fact]
    public void Equals_ReturnsTrue_ForSameValues()
    {
        // Arrange
        var left = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions",
            IdempotencyKey = "idem"
        };
        var right = new CommandDedupKey
        {
            DedupKey = "k1",
            Endpoint = "POST /v1/executions",
            IdempotencyKey = "idem"
        };

        // Act & Assert
        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>フィールドが異なるキーは等しくない。</summary>
    [Fact]
    public void Equals_ReturnsFalse_WhenDedupKeyDiffers()
    {
        // Arrange
        var left = new CommandDedupKey { DedupKey = "a", Endpoint = "e", IdempotencyKey = "i" };
        var right = new CommandDedupKey { DedupKey = "b", Endpoint = "e", IdempotencyKey = "i" };

        // Act & Assert
        Assert.False(left.Equals(right));
        Assert.False(left == right);
        Assert.True(left != right);
        Assert.False(left.Equals((object?)right));
    }

    /// <summary>別型との比較は false。</summary>
    [Fact]
    public void Equals_ReturnsFalse_ForDifferentType()
    {
        // Arrange
        var key = new CommandDedupKey { DedupKey = "k", Endpoint = "e", IdempotencyKey = "i" };

        // Act & Assert
        Assert.False(key.Equals("not-a-key"));
    }
}
