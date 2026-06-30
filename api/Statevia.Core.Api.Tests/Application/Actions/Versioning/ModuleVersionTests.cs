using Statevia.Core.Api.Application.Actions.Versioning;

namespace Statevia.Core.Api.Tests.Application.Actions.Versioning;

/// <summary><see cref="ModuleVersion"/> の解析・比較の単体テスト。</summary>
public sealed class ModuleVersionTests
{
    /// <summary>major.minor.patch の安定版を解析できる。</summary>
    [Fact]
    public void TryParse_WhenStable_ParsesComponents()
    {
        // Arrange / Act
        var parsed = ModuleVersion.TryParse("1.2.3", out var version);

        // Assert
        Assert.True(parsed);
        Assert.Equal(1, version!.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Patch);
        Assert.True(version.IsStable);
    }

    /// <summary>pre-release 付き版を解析し、安定版でないと判定する。</summary>
    [Fact]
    public void TryParse_WhenPreRelease_IsNotStable()
    {
        // Arrange / Act
        var parsed = ModuleVersion.TryParse("1.3.0-rc.1", out var version);

        // Assert
        Assert.True(parsed);
        Assert.Equal("rc.1", version!.PreRelease);
        Assert.False(version.IsStable);
    }

    /// <summary>build metadata（+ 以降）は優先順位に影響せず無視する。</summary>
    [Fact]
    public void TryParse_WhenBuildMetadata_IsIgnored()
    {
        // Arrange / Act
        var parsed = ModuleVersion.TryParse("1.2.3+build.7", out var version);

        // Assert
        Assert.True(parsed);
        Assert.Equal(new ModuleVersion(1, 2, 3), version);
    }

    /// <summary>不正な書式（要素数違反・先頭ゼロ・空 pre-release）は解析失敗とする。</summary>
    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1000000000.0.0")]
    public void TryParse_WhenInvalid_ReturnsFalse(string value)
    {
        // Act
        var parsed = ModuleVersion.TryParse(value, out var version);

        // Assert
        Assert.False(parsed);
        Assert.Null(version);
    }

    /// <summary>安定版は同一 major.minor.patch の pre-release より高い。</summary>
    [Fact]
    public void CompareTo_StableIsGreaterThanPreRelease()
    {
        // Arrange
        var stable = ModuleVersion.Parse("1.0.0");
        var preRelease = ModuleVersion.Parse("1.0.0-rc.1");

        // Act / Assert
        Assert.True(stable.CompareTo(preRelease) > 0);
        Assert.True(preRelease.CompareTo(stable) < 0);
    }

    /// <summary>pre-release 識別子は数値＜非数値、数値同士は数値比較、識別子数が多い方が高い。</summary>
    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta")]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.2")]
    public void CompareTo_PreReleasePrecedence(string lower, string higher)
    {
        // Arrange
        var low = ModuleVersion.Parse(lower);
        var high = ModuleVersion.Parse(higher);

        // Act / Assert
        Assert.True(low.CompareTo(high) < 0);
    }

    /// <summary>桁数上限ちょうど（9 桁）の版は解析できる。</summary>
    [Fact]
    public void TryParse_WhenMaxDigits_ParsesSuccessfully()
    {
        // Act
        var parsed = ModuleVersion.TryParse("999999999.0.0", out var version);

        // Assert
        Assert.True(parsed);
        Assert.Equal(999_999_999, version!.Major);
    }

    /// <summary>数値コンポーネントの比較が優先される。</summary>
    [Fact]
    public void CompareTo_NumericComponentsTakePrecedence()
    {
        // Arrange
        var lower = ModuleVersion.Parse("1.2.9");
        var higher = ModuleVersion.Parse("1.3.0");

        // Act / Assert
        Assert.True(lower.CompareTo(higher) < 0);
    }
}
