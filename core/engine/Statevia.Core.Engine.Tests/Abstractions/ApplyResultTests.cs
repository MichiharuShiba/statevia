using Statevia.Core.Engine.Abstractions;
using Xunit;

namespace Statevia.Core.Engine.Tests.Abstractions;

/// <summary><see cref="ApplyResult"/> の等価性とファクトリ。</summary>
public class ApplyResultTests
{
    /// <summary>Applied と AlreadyApplied が対称的なプロパティを持つことを検証する。</summary>
    [Fact]
    public void Applied_And_AlreadyApplied_AreDistinct()
    {
        // Arrange / Act
        var applied = ApplyResult.Applied;
        var duplicate = ApplyResult.AlreadyApplied;

        // Assert
        Assert.True(applied.IsApplied);
        Assert.False(applied.IsAlreadyApplied);
        Assert.False(duplicate.IsApplied);
        Assert.True(duplicate.IsAlreadyApplied);
    }

    /// <summary>同種の結果同士は等価、異種は非等価であることを検証する。</summary>
    [Fact]
    public void Equals_And_Operators_DistinguishAppliedFromAlreadyApplied()
    {
        // Arrange
        var a = ApplyResult.Applied;
        var b = ApplyResult.Applied;
        var c = ApplyResult.AlreadyApplied;

        // Assert
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.False(a.Equals(c));
        Assert.False(a == c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), c.GetHashCode());
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals("not-a-result"));
    }
}
