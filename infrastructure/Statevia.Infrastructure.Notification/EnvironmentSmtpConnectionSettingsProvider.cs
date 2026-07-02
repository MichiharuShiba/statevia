using Microsoft.Extensions.Options;

namespace Statevia.Infrastructure.Notification;

/// <summary>
/// 環境変数および <see cref="NotificationOptions.Smtp"/> から SMTP 接続設定を解決する。
/// </summary>
internal sealed class EnvironmentSmtpConnectionSettingsProvider : ISmtpConnectionSettingsProvider
{
    private readonly NotificationOptions _options;

    /// <summary>通知オプションを注入する。</summary>
    /// <param name="options">bind 済み <see cref="NotificationOptions"/>。</param>
    public EnvironmentSmtpConnectionSettingsProvider(IOptions<NotificationOptions> options) =>
        _options = options.Value;

    /// <inheritdoc />
    public Task<SmtpConnectionSettings> GetAsync(SmtpConnectionSettingsRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;

        var smtp = _options.Smtp;
        var host = FirstNonEmpty(Environment.GetEnvironmentVariable("STATEVIA_SMTP_HOST"), smtp.Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SMTP is not configured.");
        }

        var port = smtp.Port;
        var portText = Environment.GetEnvironmentVariable("STATEVIA_SMTP_PORT");
        if (int.TryParse(portText, out var parsedPort))
        {
            port = parsedPort;
        }

        var user = FirstNonEmpty(Environment.GetEnvironmentVariable("STATEVIA_SMTP_USER"), smtp.User);
        var password = FirstNonEmpty(Environment.GetEnvironmentVariable("STATEVIA_SMTP_PASSWORD"), smtp.Password);
        var defaultFrom = FirstNonEmpty(Environment.GetEnvironmentVariable("STATEVIA_SMTP_FROM"), smtp.DefaultFrom);

        return Task.FromResult(new SmtpConnectionSettings(host, port, user, password, defaultFrom));
    }

    private static string? FirstNonEmpty(string? primary, string? fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : primary;
}
