using System.Net;
using System.Net.Mail;

namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary>解決済み SMTP 接続設定で email 送信する。</summary>
internal sealed class SmtpNotificationSender : INotificationSender
{
    private readonly ISmtpConnectionSettingsProvider _settingsProvider;

    /// <summary>接続設定プロバイダを注入する。</summary>
    /// <param name="settingsProvider">SMTP 接続設定の解決元。</param>
    public SmtpNotificationSender(ISmtpConnectionSettingsProvider settingsProvider) =>
        _settingsProvider = settingsProvider;

    /// <inheritdoc />
    public async Task<NotificationSendResult> SendEmailAsync(NotificationEmailRequest request, CancellationToken ct)
    {
        var settings = await _settingsProvider
            .GetAsync(new SmtpConnectionSettingsRequest(), ct)
            .ConfigureAwait(false);

        using var message = new MailMessage
        {
            From = new MailAddress(request.From ?? settings.DefaultFrom ?? "noreply@statevia.local"),
            Subject = request.Subject,
            Body = request.Body,
        };
        message.To.Add(request.To);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(settings.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.User, settings.Password),
        };

        await client.SendMailAsync(message, ct).ConfigureAwait(false);
        return new NotificationSendResult("email", null);
    }
}
