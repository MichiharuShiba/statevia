using Statevia.Core.Api.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure;

public sealed class UuidV7GeneratorTests
{
    /// <summary>
    /// 生成した識別子は空値にならない。
    /// </summary>
    [Fact]
    public void NewGuid_ReturnsNonEmptyGuid()
    {
        // Arrange
        var sut = new UuidV7Generator();

        // Act
        var id = sut.NewGuid();

        // Assert
        Assert.NotEqual(Guid.Empty, id);
    }

    /// <summary>
    /// 連続生成した識別子は同じ値にならない。
    /// </summary>
    [Fact]
    public void NewGuid_ReturnsDifferentValues()
    {
        // Arrange
        var sut = new UuidV7Generator();

        // Act
        var id1 = sut.NewGuid();
        var id2 = sut.NewGuid();

        // Assert
        Assert.NotEqual(id1, id2);
    }
}
