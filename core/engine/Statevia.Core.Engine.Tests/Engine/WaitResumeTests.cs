using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class WaitResumeTests
{
    /// <summary>WaitAsync で待機中、Publish でイベントを発行すると Wait が完了することを検証する。</summary>
    [Fact]
    public async Task WaitAsync_Completes_WhenEventPublished()
    {
        // Arrange: EventProvider を作成し、WaitAsync で待機開始
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitAsync("TestEvent", CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act: Publish でイベント発行
        provider.Publish("TestEvent");

        // Assert: Wait が完了すること
        await waitTask.ConfigureAwait(false);
        Assert.True(waitTask.IsCompletedSuccessfully);
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
        await cts.CancelAsync().ConfigureAwait(false);

        // Assert: TaskCanceledException がスローされること
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitTask.ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>ノードスコープ待機は Resume で指定イベント名を返し、未使用イベントの waiter を除去する。</summary>
    [Fact]
    public async Task WaitForEventAsync_CompletesWithEventName_WhenResumed()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitForEventAsync(
            "ApproveTask",
            ["approve", "reject"],
            CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act
        provider.Resume("ApproveTask", "approve");

        // Assert
        var eventName = await waitTask.ConfigureAwait(false);
        Assert.Equal("approve", eventName);

        // 再 Resume しても完了済みのため no-op（二重再開しない）
        provider.Resume("ApproveTask", "reject");
        Assert.True(waitTask.IsCompletedSuccessfully);
        Assert.Equal("approve", waitTask.Result);
    }

    /// <summary>同一イベント名でも nodeId が異なれば独立に Resume できる。</summary>
    [Fact]
    public async Task WaitForEventAsync_AllowsSameEventName_OnDifferentNodes()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        var managerWait = provider.WaitForEventAsync("ManagerApproval", ["approve"], CancellationToken.None);
        var securityWait = provider.WaitForEventAsync("SecurityApproval", ["approve"], CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act
        provider.Resume("ManagerApproval", "approve");
        provider.Resume("SecurityApproval", "approve");

        // Assert
        Assert.Equal("approve", await managerWait.ConfigureAwait(false));
        Assert.Equal("approve", await securityWait.ConfigureAwait(false));
    }

    /// <summary>許可外イベントの Resume は待機を完了させない。</summary>
    [Fact]
    public async Task Resume_IgnoresEvent_WhenNotAllowed()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitForEventAsync("ApproveTask", ["approve"], CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // Act
        provider.Resume("ApproveTask", "reject");

        // Assert
        Assert.False(waitTask.IsCompleted);
        provider.Resume("ApproveTask", "approve");
        Assert.Equal("approve", await waitTask.ConfigureAwait(false));
    }

    /// <summary>同一 nodeId で二重 Wait すると InvalidOperationException になる。</summary>
    [Fact]
    public async Task WaitForEventAsync_Throws_WhenNodeAlreadyWaiting()
    {
        // Arrange
        var provider = new EventProvider("wf1");
        _ = provider.WaitForEventAsync("ApproveTask", ["approve"], CancellationToken.None);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WaitForEventAsync("ApproveTask", ["reject"], CancellationToken.None)).ConfigureAwait(false);
    }

    /// <summary>WaitForEventAsync 待機中に CancellationToken をキャンセルすると TaskCanceledException になる。</summary>
    [Fact]
    public async Task WaitForEventAsync_Cancels_WhenTokenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var provider = new EventProvider("wf1");
        var waitTask = provider.WaitForEventAsync("ApproveTask", ["approve"], cts.Token);

        // Act
        await cts.CancelAsync().ConfigureAwait(false);

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitTask.ConfigureAwait(false)).ConfigureAwait(false);
    }
}
