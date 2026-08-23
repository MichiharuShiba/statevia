using System.Net;
using System.Net.Mail;

namespace Statevia.Reference.Notification;

/// <summary>モジュール内で完結する SMTP 送信。</summary>
internal static class SmtpMailSender
{
    /// <summary>組み立て済みメールを SMTP で送る。</summary>
    public static async Task SendAsync(
        SmtpEnvironmentSettings settings,
        MailMessage message,
        CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(settings.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.User, settings.Password),
        };

        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
