using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="InMemoryActionCatalog"/> の版共存（version coexist）の単体テスト。</summary>
public sealed class InMemoryActionCatalogVersionCoexistTests
{
    private sealed class DummyState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult<object?>(null);
    }

    private static ActionCatalogEntry CreateEntry() =>
        new(InProcessFactory: _ => DefaultStateExecutor.Create(new DummyState()));

    private static ActionDescriptor CreateDescriptor(string moduleId, string version, string actionName) =>
        new()
        {
            ActionId = $"{moduleId}.{actionName}",
            ModuleId = moduleId,
            Version = version,
            TrustLevel = ActionTrustLevel.Community,
            Source = ActionSourceKind.Filesystem,
            OwnerTenantId = "11111111-1111-1111-1111-111111111111",
            Visibility = ActionVisibility.Tenant,
        };

    /// <summary>同一 moduleId の異なる fullVersion を共存登録できる。</summary>
    [Fact]
    public void Register_WhenSameModuleDifferentVersions_Coexists()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();

        // Act
        sut.Register(CreateDescriptor("demo.module", "1.0.0", "echo"), CreateEntry());
        sut.Register(CreateDescriptor("demo.module", "2.0.0", "echo"), CreateEntry());

        // Assert
        Assert.True(sut.TryGetRegistration("demo.module", "1.0.0", "echo", out _));
        Assert.True(sut.TryGetRegistration("demo.module", "2.0.0", "echo", out _));
        Assert.Equal(["1.0.0", "2.0.0"], sut.GetLoadedVersions("demo.module"));
    }

    /// <summary>論理 actionId 経由の lookup は単一版のみ成功し、複数版では曖昧解決しない。</summary>
    [Fact]
    public void TryGetRegistration_WhenMultipleVersionsByLogicalId_ReturnsFalse()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        sut.Register(CreateDescriptor("demo.module", "1.0.0", "echo"), CreateEntry());
        sut.Register(CreateDescriptor("demo.module", "2.0.0", "echo"), CreateEntry());

        // Act
        var exists = sut.Exists("demo.module.echo");
        var resolved = sut.TryGetRegistration("demo.module.echo", out _);

        // Assert
        Assert.True(exists);
        Assert.False(resolved);
    }

    /// <summary>版付き exact lookup は論理 actionId が同じでも版ごとに区別できる。</summary>
    [Fact]
    public void TryGetDescriptor_WhenVersionedLookup_ReturnsDistinctDescriptors()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        sut.Register(CreateDescriptor("demo.module", "1.0.0", "echo"), CreateEntry());
        sut.Register(CreateDescriptor("demo.module", "2.0.0", "echo"), CreateEntry());

        // Act
        sut.TryGetDescriptor("demo.module", "1.0.0", "echo", out var v1);
        sut.TryGetDescriptor("demo.module", "2.0.0", "echo", out var v2);

        // Assert
        Assert.Equal("1.0.0", v1!.Version);
        Assert.Equal("2.0.0", v2!.Version);
    }
}
