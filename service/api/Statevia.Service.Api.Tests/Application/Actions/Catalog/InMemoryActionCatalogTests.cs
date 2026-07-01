using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="InMemoryActionCatalog"/> の単体テスト。</summary>
public sealed class InMemoryActionCatalogTests
{
    private sealed class DummyState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static ActionDescriptor CreateTenantDescriptor(string actionId, string ownerTenantId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = "test.module",
            Version = "1.0.0",
            TrustLevel = ActionTrustLevel.Community,
            Source = ActionSourceKind.Filesystem,
            OwnerTenantId = ownerTenantId,
            Visibility = ActionVisibility.Tenant,
        };

    private static ActionCatalogEntry CreateEntry(IStateExecutor? executor = null) =>
        new(InProcessFactory: _ => executor ?? DefaultStateExecutor.Create(new DummyState()));

    /// <summary>空文字と空白文字列のアクション ID は未登録として扱う。</summary>
    [Fact]
    public void Exists_WhenWhitespace_ReturnsFalse()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();

        // Act
        var existsEmpty = sut.Exists("");
        var existsWhitespace = sut.Exists("   ");

        // Assert
        Assert.False(existsEmpty);
        Assert.False(existsWhitespace);
    }

    /// <summary>登録済み canonical actionId は存在判定できる。</summary>
    [Fact]
    public void Exists_WhenRegistered_ReturnsTrue()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        sut.Register(
            CreateTenantDescriptor("custom.action", "11111111-1111-1111-1111-111111111111"),
            CreateEntry());

        // Act
        var exists = sut.Exists("custom.action");
        var existsWithPadding = sut.Exists(" custom.action ");

        // Assert
        Assert.True(exists);
        Assert.True(existsWithPadding);
    }

    /// <summary>エイリアス経由でも存在判定と Descriptor 取得ができる。</summary>
    [Fact]
    public void TryGetDescriptor_WhenAliasRegistered_ResolvesCanonical()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        var descriptor = CreateTenantDescriptor("canonical.action", "11111111-1111-1111-1111-111111111111");
        sut.Register(descriptor, CreateEntry() with { Aliases = ["alias.action"] });

        // Act
        var ok = sut.TryGetDescriptor("alias.action", out var resolved);

        // Assert
        Assert.True(ok);
        Assert.Equal("canonical.action", resolved!.ActionId);
    }

    /// <summary>登録情報から InProcess 実行器を生成できる。</summary>
    [Fact]
    public void TryGetRegistration_WhenRegistered_ReturnsExecutorFactory()
    {
        // Arrange
        var expected = DefaultStateExecutor.Create(new DummyState());
        var sut = new InMemoryActionCatalog();
        sut.Register(
            CreateTenantDescriptor("custom.action", "11111111-1111-1111-1111-111111111111"),
            new ActionCatalogEntry(InProcessFactory: _ => expected));
        using var provider = new ServiceCollection().BuildServiceProvider();

        // Act
        var ok = sut.TryGetRegistration("custom.action", out var registration);
        var executor = ok ? registration!.Entry.InProcessFactory!(provider) : null;

        // Assert
        Assert.True(ok);
        Assert.NotNull(executor);
        Assert.Same(expected, executor);
    }

    /// <summary>同一 canonical actionId の再登録は拒否する。</summary>
    [Fact]
    public void Register_WhenDuplicateCanonicalId_Throws()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        var tenantId = "11111111-1111-1111-1111-111111111111";
        var descriptor = CreateTenantDescriptor("custom.action", tenantId);
        sut.Register(descriptor, CreateEntry());

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(
            () => sut.Register(descriptor, CreateEntry()));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>同一エイリアスを別 Action に再登録すると拒否する。</summary>
    [Fact]
    public void Register_WhenAliasConflicts_Throws()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        var tenantId = "11111111-1111-1111-1111-111111111111";
        sut.Register(
            CreateTenantDescriptor("first.action", tenantId),
            CreateEntry() with { Aliases = ["shared.alias"] });
        var second = CreateTenantDescriptor("second.action", tenantId);

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(
            () => sut.Register(second, CreateEntry() with { Aliases = ["shared.alias"] }));
        Assert.Contains("shared.alias", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>InProcessFactory 未設定は登録拒否する。</summary>
    [Fact]
    public void Register_WhenInProcessFactoryMissing_Throws()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        var descriptor = CreateTenantDescriptor("custom.action", "11111111-1111-1111-1111-111111111111");

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(
            () => sut.Register(descriptor, new ActionCatalogEntry(InProcessFactory: null!)));
        Assert.Contains("InProcessFactory is required", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Tenant Visibility で OwnerTenantId 欠落は登録拒否する。</summary>
    [Fact]
    public void Register_WhenTenantVisibilityWithoutOwner_Throws()
    {
        // Arrange
        var sut = new InMemoryActionCatalog();
        var descriptor = new ActionDescriptor
        {
            ActionId = "tenant.action",
            ModuleId = "test.module",
            Version = "1.0.0",
            Visibility = ActionVisibility.Tenant,
            OwnerTenantId = null,
        };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => sut.Register(descriptor, CreateEntry()));
    }
}
