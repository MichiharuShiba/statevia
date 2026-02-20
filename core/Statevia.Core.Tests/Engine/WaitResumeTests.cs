using Statevia.Core.Engine;
using Xunit;

namespace Statevia.Core.Tests.Engine;

public class WaitResumeTests
{
    /// <summary>WaitAsync で待機中、Publish でイベントを発行すると Wait が完了することを検証する。</summary>
    [Fact]
    public async Task WaitAsync_Completes_WhenEventPublished()
    {
        // Arrange: EventProvider を作成し、WaitAsync で待機開始
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitAsync("TestEvent", CancellationToken.None);
        await Task.Delay(50);

        // Act: Publish でイベント発行
        provider.Publish("TestEvent");

        // Assert: Wait が完了すること
        await waitTask;
    }

    /// <summary>WaitAsync 待機中に CancellationToken をキャンセルすると TaskCanceledException になることを検証する。</summary>
    [Fact]
    public async Task WaitAsync_Cancels_WhenTokenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitAsync("TestEvent", cts.Token);

        // Act: トークンをキャンセル
        await cts.CancelAsync();

        // Assert: TaskCanceledException がスローされること
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitTask);
    }
}
