using Statevia.Service.Api.Application.Actions.Versioning;

namespace Statevia.Service.Api.Tests.Application.Actions.Versioning;

/// <summary><see cref="ModuleVersionRange"/> の解析と Satisfies。</summary>
public sealed class ModuleVersionRangeTests
{
    /// <summary>空 / LATEST は最新安定レンジになる。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LATEST")]
    [InlineData("*")]
    public void Parse_LatestExpressions_AreLatest(string? expression)
    {
        // Act
        var range = ModuleVersionRange.Parse(expression);

        // Assert
        Assert.True(range.IsLatest);
        Assert.False(range.IsExact);
        Assert.True(range.Satisfies(ModuleVersion.Parse("1.2.3")));
        Assert.False(range.Satisfies(ModuleVersion.Parse("1.2.3-rc.1")));
    }

    /// <summary>exact / caret / tilde / bare を解析できる。</summary>
    [Theory]
    [InlineData("=1.2.3", true, "1.2.3", true)]
    [InlineData("1.2.3", true, "1.2.3", true)]
    [InlineData("^1.2.3", false, "1.2.4", true)]
    [InlineData("^1.2.3", false, "2.0.0", false)]
    [InlineData("~1.2.3", false, "1.2.9", true)]
    [InlineData("~1.2.3", false, "1.3.0", false)]
    [InlineData("1.2", false, "1.2.5", true)]
    [InlineData("1.x", false, "1.9.0", true)]
    [InlineData("1", false, "1.0.0", true)]
    public void Parse_CommonForms_SatisfiesExpected(
        string expression,
        bool expectExact,
        string candidate,
        bool expected)
    {
        // Arrange
        var range = ModuleVersionRange.Parse(expression);

        // Act
        var ok = range.Satisfies(ModuleVersion.Parse(candidate));

        // Assert
        Assert.Equal(expectExact, range.IsExact);
        Assert.Equal(expected, ok);
    }

    /// <summary>不正な式は FormatException。</summary>
    [Fact]
    public void Parse_InvalidExpression_ThrowsFormatException()
    {
        // Act + Assert
        Assert.Throws<FormatException>(() => ModuleVersionRange.Parse("!!"));
    }
}
