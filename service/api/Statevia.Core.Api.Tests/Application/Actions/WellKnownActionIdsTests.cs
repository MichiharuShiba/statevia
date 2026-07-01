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
        Assert.Equal(WellKnownActionIds.Sleep, canonical);
    }

    /// <summary>notify 短名は notify canonical ID に変換される。</summary>
    [Fact]
    public void ToCanonicalActionId_Notify_ReturnsNotifyFqcn()
    {
        // Act
        var canonical = WellKnownActionIds.ToCanonicalActionId("notify");

        // Assert
        Assert.Equal(WellKnownActionIds.Notify, canonical);
    }

    /// <summary>delay5s は builtin 短名ではなくなった。</summary>
    [Fact]
    public void IsBuiltinShortName_Delay5s_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(WellKnownActionIds.IsBuiltinShortName("delay5s"));
    }

    /// <summary>空白短名は ArgumentException になる。</summary>
    [Fact]
    public void ToCanonicalActionId_Whitespace_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => WellKnownActionIds.ToCanonicalActionId("   "));
    }
}
