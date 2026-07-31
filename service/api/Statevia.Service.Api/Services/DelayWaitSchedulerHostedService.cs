using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Statevia.Service.Api.Services;

/// <summary>期限切れ DelayWait を TimerFire ワークとして永続キューへ投入するスケジューラー。</summary>
internal sealed class DelayWaitSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DelayWaitSchedulerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var waits = scope.ServiceProvider.GetRequiredService<IExecutionWaitRepository>();
                var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
                var now = DateTime.UtcNow;
                var expired = await waits.ListExpiredDelayWaitsAsync(now, limit: 64, stoppingToken).ConfigureAwait(false);
                if (expired.Count > 0)
                {
                    var items = expired
                        .Select(wait => new ExecutionWorkItemRow
                        {
                            WorkItemId = Guid.NewGuid(),
                            ExecutionId = wait.ExecutionId,
                            Kind = ExecutionWorkItemKinds.TimerFire,
                            Payload = JsonSerializer.Serialize(new ExecutionResumeWorkItemPayload(
                                wait.NodeId,
                                ExecutionWaitEventNames.DelayCompleted)),
                            AvailableAt = now,
                            Attempts = 0,
                            CreatedAt = now
                        })
                        .ToList();
                    await queue.EnqueueManyAsync(items, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // バックグラウンドポーリングは反復継続のため例外種別を問わず捕捉する
            catch (Exception exception)
            {
                logger.SchedulingIterationFailed(exception);
            }
#pragma warning restore CA1031

            // 空結果でも必ず待機する（continue すると Delay を飛ばして CPU/ログを食い続ける）。
            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
