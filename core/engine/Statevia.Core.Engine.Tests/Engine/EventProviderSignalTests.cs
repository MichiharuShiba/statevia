using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="EventProvider"/> の Signal / WaitForSignalAsync 検証。</summary>
public sealed class EventProviderSignalTests
{
    /// <summary>Signal 経由で WaitForSignalAsync が再開する。</summary>
    [Fact]
    public async Task WaitForSignalAsync_Completes_WhenSignalPublished()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitForSignalAsync("approval", CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act
        provider.Signal("approval");

        // Assert
        await waitTask.ConfigureAwait(false);
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    /// <summary>PublishTopic は Signal と混同されない。</summary>
    [Fact]
    public async Task PublishTopic_DoesNotResumeWaitForSignal()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitForSignalAsync("approval", CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act
        provider.PublishTopic("payment.completed", payloadSummary: "summary");

        // Assert
        Assert.False(waitTask.IsCompleted);
        provider.Signal("approval");
        await waitTask.ConfigureAwait(false);
    }
}
