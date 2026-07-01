using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Catalog;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

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

        Assert.True(catalog.TryGetPublication(WellKnownActionIds.Rest, out var restPublication));
        Assert.NotNull(restPublication);
        Assert.Equal(WellKnownActionIds.Rest, restPublication!.Descriptor.ActionId);
        Assert.Equal("REST", restPublication.Descriptor.DisplayName);
        Assert.True(restPublication.SchemaBundle.InputSchema.RootElement.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("url", out _));
        Assert.True(properties.TryGetProperty("method", out _));
    }

    /// <summary>null Catalog は ArgumentNullException。</summary>
    [Fact]
    public void Register_NullCatalog_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => BuiltinActionRegistrar.Register(null!));
    }
}
