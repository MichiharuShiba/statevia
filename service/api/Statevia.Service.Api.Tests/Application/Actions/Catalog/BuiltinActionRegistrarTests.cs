using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Catalog;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="BuiltinActionRegistrar"/> の単体テスト。</summary>
public sealed class BuiltinActionRegistrarTests
{
    /// <summary>残す Builtin だけを Catalog へ登録し、Rest/Notify は載せない。</summary>
    [Fact]
    public void Register_RegistersRemainingBuiltinsWithoutRestOrNotify()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();

        // Act
        BuiltinActionRegistrar.Register(catalog);

        // Assert
        Assert.True(catalog.Exists(WellKnownActionIds.NoOpCanonical));
        Assert.True(catalog.Exists(WellKnownActionIds.Sleep));
        Assert.True(catalog.Exists(WellKnownActionIds.Signal));
        Assert.True(catalog.Exists(WellKnownActionIds.Publish));
        Assert.True(catalog.Exists(WellKnownActionIds.Workflow));
        Assert.False(catalog.Exists("noop"));
        Assert.False(catalog.Exists("statevia.action.builtin.rest"));
        Assert.False(catalog.Exists("statevia.action.builtin.notify"));
        Assert.False(catalog.Exists("statevia.action.reference.http.request"));

        Assert.True(catalog.TryGetDescriptor(WellKnownActionIds.NoOpCanonical, out var noop));
        Assert.Equal("statevia.action.builtin", noop!.ModuleId);
        Assert.Equal(ActionVisibility.Builtin, noop.Visibility);
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
