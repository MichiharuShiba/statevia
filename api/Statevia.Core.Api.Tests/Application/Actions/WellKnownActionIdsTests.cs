using Statevia.Core.Api.Application.Actions;

namespace Statevia.Core.Api.Tests.Application.Actions;

/// <summary><see cref="WellKnownActionIds"/> の canonical 解決ヘルパー検証。</summary>
public sealed class WellKnownActionIdsTests
{
    /// <summary>空白のみの action 参照は builtin 短名とみなさない。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsBuiltinShortName_NullOrWhitespace_ReturnsFalse(string? actionRef)
    {
        // Act & Assert
        Assert.False(WellKnownActionIds.IsBuiltinShortName(actionRef!));
    }

    /// <summary>sleep 短名は canonical FQCN に変換される。</summary>
    [Fact]
    public void ToCanonicalActionId_Sleep_ReturnsBuiltinFqcn()
    {
        // Act
        var canonical = WellKnownActionIds.ToCanonicalActionId("sleep");

        // Assert
        Assert.Equal("statevia.action.builtin.sleep", canonical);
    }

    /// <summary>delay5s は legacy ID のまま維持される。</summary>
    [Fact]
    public void ToCanonicalActionId_Delay5s_ReturnsLegacyId()
    {
        // Act
        var canonical = WellKnownActionIds.ToCanonicalActionId("delay5s");

        // Assert
        Assert.Equal(WellKnownActionIds.Delay5s, canonical);
    }

    /// <summary>空白短名は ArgumentException になる。</summary>
    [Fact]
    public void ToCanonicalActionId_Whitespace_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WellKnownActionIds.ToCanonicalActionId("   "));
    }
}
