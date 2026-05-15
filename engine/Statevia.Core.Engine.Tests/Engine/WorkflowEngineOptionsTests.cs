using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class WorkflowEngineOptionsTests
{
    /// <summary>既定の <see cref="WorkflowEngineOptions.MaxParallelism"/> が 4 であることを検証する。</summary>
    [Fact]
    public void Default_MaxParallelism_IsFour()
    {
        // Arrange & Act
        var options = new WorkflowEngineOptions();

        // Assert
        Assert.Equal(4, options.MaxParallelism);
    }

    /// <summary><see cref="WorkflowEngineOptions.MaxParallelism"/> を変更して保持されることを検証する。</summary>
    [Fact]
    public void MaxParallelism_CanBeOverridden()
    {
        // Arrange
        var options = new WorkflowEngineOptions();

        // Act
        options.MaxParallelism = 16;

        // Assert
        Assert.Equal(16, options.MaxParallelism);
    }
}
