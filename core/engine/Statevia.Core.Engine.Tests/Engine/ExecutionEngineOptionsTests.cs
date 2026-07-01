using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class ExecutionEngineOptionsTests
{
    /// <summary>既定の <see cref="ExecutionEngineOptions.MaxParallelism"/> が 4 であることを検証する。</summary>
    [Fact]
    public void Default_MaxParallelism_IsFour()
    {
        // Arrange & Act
        var options = new ExecutionEngineOptions();

        // Assert
        Assert.Equal(4, options.MaxParallelism);
    }

    /// <summary><see cref="ExecutionEngineOptions.MaxParallelism"/> を変更して保持されることを検証する。</summary>
    [Fact]
    public void MaxParallelism_CanBeOverridden()
    {
        // Arrange
        var options = new ExecutionEngineOptions();

        // Act
        options.MaxParallelism = 16;

        // Assert
        Assert.Equal(16, options.MaxParallelism);
    }
}
