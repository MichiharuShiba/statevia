using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Tests.Persistence;

/// <summary><see cref="DesignTimeDbContextFactory"/> の検証。</summary>
public sealed class DesignTimeDbContextFactoryTests
{
    /// <summary>EF CLI 用ファクトリが DbContext を生成する。</summary>
    [Fact]
    public void CreateDbContext_ReturnsCoreDbContext()
    {
        // Arrange
        var factory = new DesignTimeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.NotNull(context);
    }
}
