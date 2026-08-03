using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Statevia.Service.Api.Services;

/// <summary>
/// checkpoint 所有 lease の期限切れをスキャンし、<c>Resume mode=recovery</c> を投入するスケジューラー。
/// </summary>
/// <remarks>
/// <para>DelayWait 期限のスキャンは <see cref="DelayWaitSchedulerHostedService"/> が担う。</para>
/// <para>複数 API プロセスでも <c>FOR UPDATE SKIP LOCKED</c> により二重投入を避ける。</para>
/// <para>
/// 1 回の投入件数が <see cref="BatchLimit"/> 未満なら <see cref="IdlePollInterval"/> 待機する。
/// 上限いっぱいなら残件がある可能性が高いため待機せず次イテレーションへ進む。
/// </para>
/// </remarks>
internal sealed class ExecutionOwnershipRecoveryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExecutionOwnershipRecoveryHostedService> logger) : BackgroundService
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
                var recoveryCount = await queue.EnqueueExpiredOwnershipRecoveriesAsync(
                        DateTime.UtcNow,
                        BatchLimit,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (recoveryCount > 0)
                    logger.ExpiredOwnershipRecoveriesEnqueued(recoveryCount);

                // 上限いっぱいなら残バッチがある可能性が高いので idle 待機をスキップする。
                waitBeforeNextPoll = recoveryCount < BatchLimit;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // バックグラウンドポーリングは反復継続のため例外種別を問わず捕捉する
            catch (Exception exception)
            {
                logger.RecoveryIterationFailed(exception);
                waitBeforeNextPoll = true;
            }
#pragma warning restore CA1031

            if (waitBeforeNextPoll)
                await Task.Delay(IdlePollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
