using Statevia.Core.Api.Bootstrap;

namespace Statevia.Core.Api.Tests.Bootstrap;

/// <summary><see cref="CliArgReader"/> の検証。</summary>
public sealed class CliArgReaderTests
{
    /// <summary>次トークンを値として返す。</summary>
    [Fact]
    public void RequireValue_WithNextToken_ReturnsValue()
    {
        // Arrange
        var args = new[] { "--tenant-key", "acme" };
        var index = 0;

        // Act
        var value = CliArgReader.RequireValue(args, ref index, "--tenant-key");

        // Assert
        Assert.Equal("acme", value);
        Assert.Equal(1, index);
    }

    /// <summary>値が無いときは例外。</summary>
    [Fact]
    public void RequireValue_MissingValue_Throws()
    {
        // Arrange
        var args = new[] { "--tenant-key" };
        var index = 0;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            CliArgReader.RequireValue(args, ref index, "--tenant-key"));
        Assert.Contains("--tenant-key", ex.Message, StringComparison.Ordinal);
    }
}
