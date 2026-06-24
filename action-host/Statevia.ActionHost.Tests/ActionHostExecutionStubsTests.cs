using Statevia.ActionHost.Execution;

namespace Statevia.ActionHost.Tests;

/// <summary>OutOfProcess 実行スタブの単体テスト。</summary>
public sealed class ActionHostExecutionStubsTests
{
    /// <summary><see cref="EmptyEventProvider"/> は Wait をサポートしない。</summary>
    [Fact]
    public async Task EmptyEventProvider_WaitAsync_ThrowsNotSupported()
    {
        // Arrange
        var provider = new EmptyEventProvider();

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.WaitAsync("evt", CancellationToken.None));

        // Assert
        Assert.Contains("Event wait", exception.Message, StringComparison.Ordinal);
    }

    /// <summary><see cref="EmptyEventProvider"/> は Signal をサポートしない。</summary>
    [Fact]
    public void EmptyEventProvider_Signal_ThrowsNotSupported()
    {
        // Arrange
        var provider = new EmptyEventProvider();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => provider.Signal("sig"));

        // Assert
        Assert.Contains("Event signal", exception.Message, StringComparison.Ordinal);
    }

    /// <summary><see cref="EmptyEventProvider"/> は Topic 公開をサポートしない。</summary>
    [Fact]
    public void EmptyEventProvider_PublishTopic_ThrowsNotSupported()
    {
        // Arrange
        var provider = new EmptyEventProvider();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => provider.PublishTopic("topic", null));

        // Assert
        Assert.Contains("Topic publish", exception.Message, StringComparison.Ordinal);
    }

    /// <summary><see cref="EmptyStateStore"/> は常に出力なしを返す。</summary>
    [Fact]
    public void EmptyStateStore_TryGetOutput_ReturnsFalse()
    {
        // Arrange
        var store = new EmptyStateStore();

        // Act
        var found = store.TryGetOutput("AnyState", out var output);

        // Assert
        Assert.False(found);
        Assert.Null(output);
    }
}
