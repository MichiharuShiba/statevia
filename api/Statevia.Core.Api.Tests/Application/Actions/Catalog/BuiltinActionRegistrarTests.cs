using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Catalog;

namespace Statevia.Core.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="BuiltinActionRegistrar"/> の単体テスト。</summary>
public sealed class BuiltinActionRegistrarTests
{
    /// <summary>全 Builtin と noop エイリアスを Catalog へ登録する。</summary>
    [Fact]
    public void Register_RegistersAllBuiltinsWithMetadata()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();

        // Act
        BuiltinActionRegistrar.Register(catalog);

        // Assert
        Assert.True(catalog.Exists(WellKnownActionIds.NoOpCanonical));
        Assert.True(catalog.Exists(WellKnownActionIds.NoOp));
        Assert.True(catalog.Exists(WellKnownActionIds.Sleep));
        Assert.True(catalog.Exists(WellKnownActionIds.Rest));
        Assert.True(catalog.Exists(WellKnownActionIds.Notify));
        Assert.True(catalog.Exists(WellKnownActionIds.Signal));
        Assert.True(catalog.Exists(WellKnownActionIds.Publish));
        Assert.True(catalog.Exists(WellKnownActionIds.Workflow));
        Assert.False(catalog.Exists("delay5s"));

        Assert.True(catalog.TryGetDescriptor(WellKnownActionIds.NoOpCanonical, out var noop));
        Assert.Equal(ActionVisibility.Builtin, noop!.Visibility);
        Assert.Null(noop.OwnerTenantId);
        Assert.Equal(ActionExecutionMode.InProcess, noop.ExecutionHints.PreferredMode);

        Assert.True(catalog.TryGetCapabilityMetadata(WellKnownActionIds.Sleep, out var sleepMetadata));
        Assert.Equal(ActionCapabilityCategory.Timing, sleepMetadata!.Category);
        Assert.True(catalog.TryGetCapabilityMetadata(WellKnownActionIds.Workflow, out var workflowMetadata));
        Assert.True(workflowMetadata!.IsExperimental);
    }

    /// <summary>null Catalog は ArgumentNullException。</summary>
    [Fact]
    public void Register_NullCatalog_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => BuiltinActionRegistrar.Register(null!));
    }
}
