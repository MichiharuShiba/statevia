using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Statevia.Service.Api.Services;

/// <summary>
/// DelayWait 期限をスキャンし、<c>Resume mode=event</c>（delay.completed）を投入するスケジューラー。
/// </summary>
/// <remarks>
/// <para>checkpoint 所有の期限切れ recovery は <see cref="ExecutionOwnershipRecoveryHostedService"/> が担う。</para>
/// <para>
/// 1 回の取得件数が <see cref="BatchLimit"/> 未満なら <see cref="IdlePollInterval"/> 待機する。
/// 上限いっぱいなら残件がある可能性が高いため待機せず次イテレーションへ進む。
/// </para>
/// </remarks>
internal sealed class DelayWaitSchedulerHostedService(
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
                var waits = scope.ServiceProvider.GetRequiredService<IExecutionWaitRepository>();
                var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
                var now = DateTime.UtcNow;
                var expired = await waits.ListExpiredDelayWaitsAsync(now, BatchLimit, stoppingToken).ConfigureAwait(false);
                if (expired.Count > 0)
                {
                    var items = expired
                        .Select(wait => new ExecutionWorkItemRow
                        {
                            WorkItemId = Guid.NewGuid(),
                            ExecutionId = wait.ExecutionId,
                            Kind = ExecutionWorkItemKinds.Resume,
                            Payload = JsonSerializer.Serialize(new ExecutionResumeWorkItemPayload(
                                ExecutionResumeWorkItemModes.Event,
                                wait.NodeId,
                                ExecutionWaitEventNames.DelayCompleted)),
                            AvailableAt = now,
                            Attempts = 0,
                            CreatedAt = now
                        })
                        .ToList();
                    await queue.EnqueueManyAsync(items, stoppingToken).ConfigureAwait(false);
                }

                // 上限いっぱいなら残バッチがある可能性が高いので idle 待機をスキップする。
                waitBeforeNextPoll = expired.Count < BatchLimit;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // バックグラウンドポーリングは反復継続のため例外種別を問わず捕捉する
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
