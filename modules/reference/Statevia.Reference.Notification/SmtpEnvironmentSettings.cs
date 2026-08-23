namespace Statevia.Reference.Notification;

/// <summary>環境変数 <c>STATEVIA_SMTP_*</c> から SMTP 接続設定を読む。</summary>
internal sealed record SmtpEnvironmentSettings(
    string Host,
    int Port,
    string? User,
    string? Password,
    string? DefaultFrom)
{
    private const int DefaultPort = 587;

    /// <summary>環境変数から設定を解決する。</summary>
    /// <exception cref="InvalidOperationException">HOST が無いとき。</exception>
    public static SmtpEnvironmentSettings Read()
    {
        var host = Environment.GetEnvironmentVariable("STATEVIA_SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SMTP is not configured. Set STATEVIA_SMTP_HOST.");
        }

        var port = DefaultPort;
        var portText = Environment.GetEnvironmentVariable("STATEVIA_SMTP_PORT");
        if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsedPort))
        {
            port = parsedPort;
        }

        return new SmtpEnvironmentSettings(
            host.Trim(),
            port,
            EmptyToNull(Environment.GetEnvironmentVariable("STATEVIA_SMTP_USER")),
            EmptyToNull(Environment.GetEnvironmentVariable("STATEVIA_SMTP_PASSWORD")),
            EmptyToNull(Environment.GetEnvironmentVariable("STATEVIA_SMTP_FROM")));
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
