using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Runtime.Services;

/// <summary>
/// DelayWait 期限をスキャンし、<c>Resume mode=event</c>（delay.completed）を投入するスケジューラー。
/// </summary>
/// <remarks>
/// <para>
/// 排他は <see cref="IExecutionWorkQueue.EnqueueExpiredDelayWaitResumesAsync"/> の
/// <c>FOR UPDATE SKIP LOCKED</c> で担保する（list+enqueue の非排他経路は使わない）。
/// </para>
/// <para>checkpoint 所有の期限切れ recovery は <see cref="ExecutionOwnershipRecoveryHostedService"/> が担う。</para>
/// <para>
/// 1 回の投入件数がバッチ上限未満なら idle 待機する。
/// 上限いっぱいなら残件がある可能性が高いため待機せず次イテレーションへ進む。
/// </para>
/// </remarks>
public sealed class DelayWaitSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DelayWaitSchedulerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(5);
    private const int BatchLimit = 64;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var waitBeforeNextPoll = true;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
                var count = await queue.EnqueueExpiredDelayWaitResumesAsync(
                    DateTime.UtcNow, BatchLimit, stoppingToken).ConfigureAwait(false);
                if (count > 0)
                    logger.ExpiredDelayWaitResumesEnqueued(count);
                waitBeforeNextPoll = count < BatchLimit;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
            catch (Exception exception)
            {
                logger.SchedulingIterationFailed(exception);
                waitBeforeNextPoll = true;
            }
#pragma warning restore CA1031
            if (waitBeforeNextPoll)
                await Task.Delay(IdlePollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
