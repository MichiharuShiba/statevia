using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Runtime.Services;

/// <summary>期限切れ checkpoint 所有権の回復 work item を投入する。</summary>
public sealed class ExecutionOwnershipRecoveryHostedService(
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
                var count = await queue.EnqueueExpiredOwnershipRecoveriesAsync(
                    DateTime.UtcNow, BatchLimit, stoppingToken).ConfigureAwait(false);
                if (count > 0)
                    logger.ExpiredOwnershipRecoveriesEnqueued(count);
                waitBeforeNextPoll = count < BatchLimit;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
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
