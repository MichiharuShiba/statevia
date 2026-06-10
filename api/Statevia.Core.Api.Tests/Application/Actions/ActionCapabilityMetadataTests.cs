using Statevia.Core.Api.Application.Actions;

namespace Statevia.Core.Api.Tests.Application.Actions;

/// <summary><see cref="ActionCapabilityMetadata"/> と <see cref="ActionCapabilityCategory"/> の契約検証。</summary>
public sealed class ActionCapabilityMetadataTests
{
    /// <summary>Capability メタデータの各プロパティが保持される。</summary>
    [Fact]
    public void Constructor_SetsCategoryDisplayNameAndExperimentalFlag()
    {
        // Arrange & Act
        var metadata = new ActionCapabilityMetadata(
            ActionCapabilityCategory.Timing,
            "Sleep",
            IsExperimental: false);

        // Assert
        Assert.Equal(ActionCapabilityCategory.Timing, metadata.Category);
        Assert.Equal("Sleep", metadata.DisplayName);
        Assert.False(metadata.IsExperimental);
    }

    /// <summary>experimental フラグを true に設定できる。</summary>
    [Fact]
    public void Constructor_ExperimentalWorkflowSync_SetsFlag()
    {
        // Arrange & Act
        var metadata = new ActionCapabilityMetadata(
            ActionCapabilityCategory.Workflow,
            "Workflow (sync)",
            IsExperimental: true);

        // Assert
        Assert.Equal(ActionCapabilityCategory.Workflow, metadata.Category);
        Assert.True(metadata.IsExperimental);
    }
}
