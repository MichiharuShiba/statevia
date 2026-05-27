using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Infrastructure;
using Xunit;

namespace Statevia.Core.Engine.Tests.Infrastructure;

public class DelegateExecutionIdGeneratorTests
{
    /// <summary>登録したデリゲートの戻り値がそのまま ID として返ることを検証する。</summary>
    [Fact]
    public void NewExecutionId_ReturnsDelegateResult()
    {
        // Arrange
        var generator = new DelegateExecutionIdGenerator(() => "fixed-id");

        // Act
        var id = generator.NewExecutionId();

        // Assert
        Assert.Equal("fixed-id", id);
    }

    /// <summary>デリゲートが null のときコンストラクタが <see cref="ArgumentNullException"/> をスローすることを検証する。</summary>
    [Fact]
    public void Constructor_NullDelegate_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new DelegateExecutionIdGenerator(null!));
    }
}
