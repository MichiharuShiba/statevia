using Statevia.Core.Api.Application.Actions.Registry;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Tests.Application.Actions.Registry;

public sealed class InMemoryActionRegistryTests
{
    private sealed class DummyExecutor : IStateExecutor
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// 空文字と空白文字列のアクションIDは未登録として扱う。
    /// </summary>
    [Fact]
    public void Exists_WhenWhitespace_ReturnsFalse()
    {
        // Arrange
        var sut = new InMemoryActionRegistry();

        // Act
        var existsEmpty = sut.Exists("");
        var existsWhitespace = sut.Exists("   ");

        // Assert
        Assert.False(existsEmpty);
        Assert.False(existsWhitespace);
    }
    /// <summary>
    /// 登録済みアクションIDは前後空白を除去して存在判定できる。
    /// </summary>
    [Fact]
    public void Exists_WhenRegistered_ReturnsTrue()
    {
        // Arrange
        var sut = new InMemoryActionRegistry();
        sut.Register("custom.action", new DummyExecutor());

        // Act
        var exists = sut.Exists("custom.action");
        var existsWithPadding = sut.Exists(" custom.action ");

        // Assert
        Assert.True(exists);
        Assert.True(existsWithPadding);
    }
    /// <summary>
    /// 空白のみのIDを解決すると失敗し実行器はnullになる。
    /// </summary>
    [Fact]
    public void TryResolve_WhenWhitespace_ReturnsFalse_AndNullExecutor()
    {
        // Arrange
        var sut = new InMemoryActionRegistry();

        // Act
        var ok = sut.TryResolve("  ", out var executor);
        // Assert
        Assert.False(ok);
        Assert.Null(executor);
    }

    /// <summary>
    /// 登録済みIDの解決では同一の実行器インスタンスを返す。
    /// </summary>
    [Fact]
    public void TryResolve_WhenRegistered_ReturnsTrue_AndExecutor()
    {
        // Arrange
        var sut = new InMemoryActionRegistry();
        var exec = new DummyExecutor();
        sut.Register("custom.action", exec);

        // Act
        var ok = sut.TryResolve("custom.action", out var executor);
        // Assert
        Assert.True(ok);
        Assert.NotNull(executor);
        Assert.Same(exec, executor);
    }
}

