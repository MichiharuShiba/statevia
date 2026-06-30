using Statevia.Core.Api.Application.Actions.Versioning;

namespace Statevia.Core.Api.Tests.Application.Actions.Versioning;

/// <summary><see cref="ModuleVersionResolver"/> の版解決規則（npm 準拠）の単体テスト。</summary>
public sealed class ModuleVersionResolverTests
{
    private static ModuleVersion[] Versions(params string[] values) =>
        values.Select(ModuleVersion.Parse).ToArray();

    /// <summary>省略（LATEST）はロード済みの最新安定版へ解決し、pre-release を除外する。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("LATEST")]
    [InlineData("*")]
    public void Resolve_WhenLatest_PicksHighestStable(string range)
    {
        // Arrange
        var available = Versions("1.0.0", "1.2.0", "2.0.0", "2.1.0-rc.1");

        // Act
        var resolved = ModuleVersionResolver.Resolve(range, available);

        // Assert
        Assert.Equal("2.0.0", resolved.ToString());
    }

    /// <summary>メジャーのみ指定（X-range）は当該メジャー内の最新安定版へ解決する。</summary>
    [Fact]
    public void Resolve_WhenMajorOnly_PicksHighestStableWithinMajor()
    {
        // Arrange
        var available = Versions("1.0.0", "1.4.2", "1.5.0", "2.0.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve("1", available);

        // Assert
        Assert.Equal("1.5.0", resolved.ToString());
    }

    /// <summary>ベア 2 要素（1.2）は 1.2.x の最新安定版へ解決する。</summary>
    [Fact]
    public void Resolve_WhenBareMinor_ResolvesToPatchRange()
    {
        // Arrange
        var available = Versions("1.2.0", "1.2.7", "1.3.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve("1.2", available);

        // Assert
        Assert.Equal("1.2.7", resolved.ToString());
    }

    /// <summary>caret（^1.2）は >=1.2.0 &lt;2.0.0 を満たす最新安定版へ解決する。</summary>
    [Fact]
    public void Resolve_WhenCaret_AllowsMinorAndPatchUpgrades()
    {
        // Arrange
        var available = Versions("1.1.0", "1.2.0", "1.9.9", "2.0.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve("^1.2", available);

        // Assert
        Assert.Equal("1.9.9", resolved.ToString());
    }

    /// <summary>caret（^0.2）は pre-1.0 の最左非ゼロ固定で >=0.2.0 &lt;0.3.0 となる。</summary>
    [Fact]
    public void Resolve_WhenCaretPreOne_LocksMinor()
    {
        // Arrange
        var available = Versions("0.2.0", "0.2.5", "0.3.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve("^0.2", available);

        // Assert
        Assert.Equal("0.2.5", resolved.ToString());
    }

    /// <summary>tilde（~1.2）は >=1.2.0 &lt;1.3.0 を満たす最新安定版へ解決する。</summary>
    [Fact]
    public void Resolve_WhenTilde_AllowsPatchUpgradesOnly()
    {
        // Arrange
        var available = Versions("1.2.0", "1.2.9", "1.3.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve("~1.2", available);

        // Assert
        Assert.Equal("1.2.9", resolved.ToString());
    }

    /// <summary>exact 指定（=1.2.3）は当該版そのものへ解決する。</summary>
    [Fact]
    public void Resolve_WhenExact_PicksThatVersion()
    {
        // Arrange
        var available = Versions("1.2.2", "1.2.3", "1.2.4");

        // Act
        var resolved = ModuleVersionResolver.Resolve("=1.2.3", available);

        // Assert
        Assert.Equal("1.2.3", resolved.ToString());
    }

    /// <summary>pre-release は exact 指定でのみ選択できる。</summary>
    [Fact]
    public void Resolve_WhenExactPreRelease_SelectsPreRelease()
    {
        // Arrange
        var available = Versions("1.2.0", "1.3.0-rc.1");

        // Act
        var resolved = ModuleVersionResolver.Resolve("=1.3.0-rc.1", available);

        // Assert
        Assert.Equal("1.3.0-rc.1", resolved.ToString());
    }

    /// <summary>レンジ指定では pre-release を除外する（安定版優先）。</summary>
    [Fact]
    public void Resolve_WhenRangeAndOnlyPreReleaseHigher_PicksStable()
    {
        // Arrange
        var available = Versions("1.2.0", "1.3.0-rc.1");

        // Act
        var resolved = ModuleVersionResolver.Resolve("^1.2", available);

        // Assert
        Assert.Equal("1.2.0", resolved.ToString());
    }

    /// <summary>レンジを満たす版が無ければ解決失敗（暗黙の別版選択はしない）。</summary>
    [Fact]
    public void Resolve_WhenNoMatch_Throws()
    {
        // Arrange
        var available = Versions("1.0.0", "1.1.0");

        // Act / Assert
        Assert.Throws<ModuleVersionResolutionException>(
            () => ModuleVersionResolver.Resolve("^2.0", available));
    }

    /// <summary>exact 指定版が未ロードなら解決失敗とする。</summary>
    [Fact]
    public void Resolve_WhenExactNotLoaded_Throws()
    {
        // Arrange
        var available = Versions("1.2.0", "1.2.1");

        // Act / Assert
        Assert.Throws<ModuleVersionResolutionException>(
            () => ModuleVersionResolver.Resolve("=1.2.5", available));
    }

    /// <summary>レンジの数値要素が桁数上限を超える場合は書式エラーとする（オーバーフロー防止）。</summary>
    [Fact]
    public void Resolve_WhenRangeComponentExceedsDigitLimit_Throws()
    {
        // Arrange
        var available = Versions("1.0.0");

        // Act / Assert
        Assert.Throws<FormatException>(
            () => ModuleVersionResolver.Resolve("^1000000000", available));
    }

    /// <summary>ModuleReference を解決し、不変の確定参照（alias / moduleId / 確定版）を返す。</summary>
    [Fact]
    public void Resolve_Reference_ProducesResolvedReference()
    {
        // Arrange
        var reference = new ModuleReference("mail", "statevia.mail", "^1.0");
        var available = Versions("1.0.0", "1.4.0");

        // Act
        var resolved = ModuleVersionResolver.Resolve(reference, available);

        // Assert
        Assert.Equal(new ResolvedModuleReference("mail", "statevia.mail", "1.4.0"), resolved);
    }
}
