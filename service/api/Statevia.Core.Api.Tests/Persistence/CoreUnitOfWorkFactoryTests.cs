using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Persistence;

/// <summary><see cref="CoreUnitOfWorkFactory"/> の検証。</summary>
public sealed class CoreUnitOfWorkFactoryTests
{
    /// <summary>CreateAsync は CoreUnitOfWork を返す。</summary>
    [Fact]
    public async Task CreateAsync_ReturnsCoreUnitOfWork()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var factory = new CoreUnitOfWorkFactory(database.Factory);

        // Act
        await using var uow = await factory.CreateAsync();

        // Assert
        Assert.IsType<CoreUnitOfWork>(uow);
    }
}
