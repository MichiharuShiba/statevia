using Statevia.ActionHost.Modules;

namespace Statevia.ActionHost.Tests;

/// <summary><see cref="ActionHostActionRegistry"/> の単体テスト。</summary>
public sealed class ActionHostActionRegistryTests
{
    /// <summary>同一 actionId の再登録は拒否される。</summary>
    [Fact]
    public void TryRegister_WhenActionIdAlreadyExists_ReturnsFalse()
    {
        // Arrange
        var registry = new ActionHostActionRegistry();
        var first = CreateRegistration("test.module.echo");
        var second = CreateRegistration("test.module.echo");

        // Act
        var firstRegistered = registry.TryRegister(first);
        var secondRegistered = registry.TryRegister(second);

        // Assert
        Assert.True(firstRegistered);
        Assert.False(secondRegistered);
        Assert.Equal(1, registry.Count);
    }

    private static LoadedActionRegistration CreateRegistration(string actionId) =>
        new(actionId, new StubExecutor(), "test.module");

    private sealed class StubExecutor : Statevia.Core.Engine.Abstractions.IStateExecutor
    {
        public Task<object?> ExecuteAsync(
            Statevia.Core.Engine.Abstractions.StateContext ctx,
            object? input,
            CancellationToken ct) =>
            Task.FromResult(input);
    }
}
