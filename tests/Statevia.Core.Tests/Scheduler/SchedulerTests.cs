using Statevia.Core.Scheduler;
using Xunit;

namespace Statevia.Core.Tests.Scheduler;

public class SchedulerTests
{
    /// <summary>ExecutionLimiter が maxParallelism を超えて同時実行しないことを検証する。</summary>
    [Fact]
    public async Task ExecutionLimiter_LimitsConcurrency()
    {
        // Arrange: 最大 2 並列のリミッターと、同時実行数を計測する変数
        var limiter = new ExecutionLimiter(2);
        var concurrent = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        // Act: 5 件のタスクを同時に投入
        var tasks = Enumerable.Range(0, 5).Select(_ => limiter.RunAsync(async ct =>
        {
            lock (lockObj) { concurrent++; maxConcurrent = Math.Max(maxConcurrent, concurrent); }
            await Task.Delay(50, ct);
            lock (lockObj) { concurrent--; }
            return 1;
        }));

        await Task.WhenAll(tasks);

        // Assert: 同時実行数の最大値が 2 であること
        Assert.Equal(2, maxConcurrent);
    }

    /// <summary>DefaultScheduler が渡した Func を実行して結果を返すことを検証する。</summary>
    [Fact]
    public async Task DefaultScheduler_RunsWork()
    {
        // Arrange
        var scheduler = new DefaultScheduler(2);

        // Act
        var result = await scheduler.RunAsync(ct => Task.FromResult(42));

        // Assert
        Assert.Equal(42, result);
    }
}
