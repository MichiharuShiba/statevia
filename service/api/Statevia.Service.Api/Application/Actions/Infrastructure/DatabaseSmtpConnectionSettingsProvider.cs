namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary>
/// テナント DB 等から SMTP 接続設定を解決する（未実装プレースホルダ）。
/// </summary>
internal sealed class DatabaseSmtpConnectionSettingsProvider : ISmtpConnectionSettingsProvider
{
    /// <inheritdoc />
    public Task<SmtpConnectionSettings> GetAsync(SmtpConnectionSettingsRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException(
            "Notification SMTP settings source 'Database' is not implemented. "
            + "Set Notification:SmtpSettingsSource or STATEVIA_NOTIFICATION_SMTP_SOURCE to Environment.");
    }
}
