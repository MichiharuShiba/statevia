using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Catalog;

namespace Statevia.Core.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="BuiltinActionRegistrar"/> の単体テスト。</summary>
public sealed class BuiltinActionRegistrarTests
{
    /// <summary>noop / delay5s と noop エイリアスを Catalog へ登録する。</summary>
    [Fact]
    public void Register_RegistersBuiltinsAndNoOpAlias()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();

        // Act
        BuiltinActionRegistrar.Register(catalog);

        // Assert
        Assert.True(catalog.Exists(WellKnownActionIds.NoOpCanonical));
        Assert.True(catalog.Exists(WellKnownActionIds.NoOp));
        Assert.True(catalog.Exists(WellKnownActionIds.Delay5s));

        Assert.True(catalog.TryGetDescriptor(WellKnownActionIds.NoOpCanonical, out var noop));
        Assert.Equal(ActionVisibility.Builtin, noop!.Visibility);
        Assert.Null(noop.OwnerTenantId);
        Assert.Equal(ActionExecutionMode.InProcess, noop.ExecutionHints.PreferredMode);
    }

    /// <summary>null Catalog は ArgumentNullException。</summary>
    [Fact]
    public void Register_NullCatalog_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => BuiltinActionRegistrar.Register(null!));
    }
}
