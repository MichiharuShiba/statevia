using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;
using System.Net.Mail;

namespace Statevia.Reference.Notification;

/// <summary>email 通知を送るリファレンス Action。</summary>
internal sealed class NotificationSendActionState : IState<object?, object?>
{
    private readonly Func<MailMessage, CancellationToken, Task> _sendAsync;

    /// <summary>環境変数の SMTP 設定で送信する。</summary>
    public NotificationSendActionState()
        : this(static (message, cancellationToken) =>
            SmtpMailSender.SendAsync(SmtpEnvironmentSettings.Read(), message, cancellationToken))
    {
    }

    /// <summary>テスト用に送信処理を差し替える。</summary>
    /// <param name="sendAsync">組み立て済みメールを送るデリゲート。</param>
    internal NotificationSendActionState(Func<MailMessage, CancellationToken, Task> sendAsync) =>
        _sendAsync = sendAsync;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        _ = ctx;
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("notification.send action requires input fields.");
        }

        var channel = ActionInputReader.RequireString(fields, "channel");
        if (!string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("notification.send action supports channel=email only.");
        }

        var to = ActionInputReader.RequireString(fields, "to");
        var subject = ActionInputReader.RequireString(fields, "subject");
        var body = ActionInputReader.RequireString(fields, "body");
        var from = ActionInputReader.OptionalString(fields, "from");
        using var message = CreateMessage(to, subject, body, from);
        await _sendAsync(message, ct).ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["messageId"] = null,
        };
    }

    private static MailMessage CreateMessage(string to, string subject, string body, string? from)
    {
        var fromAddress = from;
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = SmtpEnvironmentSettings.Read().DefaultFrom;
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException(
                "SMTP from address is not configured. Set STATEVIA_SMTP_FROM or input.from.");
        }

        var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = subject,
            Body = body,
        };
        message.To.Add(to);
        return message;
    }
}
