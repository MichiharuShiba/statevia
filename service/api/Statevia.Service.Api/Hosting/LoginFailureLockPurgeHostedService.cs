using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Hosting;

/// <summary>期限切れのログイン失敗行を定期 DELETE する。</summary>
/// <param name="store">ロック表の削除処理。</param>
/// <param name="logger">周期実行の失敗ログ。</param>
/// <remarks>周期は失敗窓と同じ 15 分。ログイン経路には載せない。</remarks>
internal sealed class LoginFailureLockPurgeHostedService(
    LoginFailureLockStore store,
    ILogger<LoginFailureLockPurgeHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                store.PurgeExpired();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
            catch (Exception exception)
            {
                logger.PurgeFailed(exception);
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(LoginFailureLockStore.FailureWindow, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

/// <summary>
/// <see cref="LoginFailureLockPurgeHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class LoginFailureLockPurgeHostedServiceLogMessages
{
    /// <summary>定期削除の 1 回が失敗した。次周期で再試行する。</summary>
    [LoggerMessage(
        EventId = 4052,
        Level = LogLevel.Error,
        Message = "Failed to purge expired login failure rows.")]
    public static partial void PurgeFailed(this ILogger logger, Exception exception);
}
