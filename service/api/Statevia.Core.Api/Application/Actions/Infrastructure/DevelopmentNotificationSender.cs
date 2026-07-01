namespace Statevia.Core.Api.Application.Actions.Infrastructure;

/// <summary>Development 向け no-op 通知（Warning ログのみ）。</summary>
internal sealed class DevelopmentNotificationSender : INotificationSender
{
    private readonly ILogger<DevelopmentNotificationSender> _logger;

    /// <summary>ロガー付きで構築する。</summary>
    public DevelopmentNotificationSender(ILogger<DevelopmentNotificationSender> logger) =>
        _logger = logger;

    /// <inheritdoc />
    public Task<NotificationSendResult> SendEmailAsync(NotificationEmailRequest request, CancellationToken ct)
    {
        DevelopmentNotificationLogMessages.NotificationSkippedInDevelopment(
            _logger,
            request.To.Length,
            request.Subject.Length);
        return Task.FromResult(new NotificationSendResult("email", "development-skipped"));
    }
}

/// <summary>環境に応じた <see cref="INotificationSender"/> を選択する。</summary>
internal sealed class NotificationSenderResolver
{
    private readonly IHostEnvironment _environment;
    private readonly DevelopmentNotificationSender _developmentSender;
    private readonly SmtpNotificationSender _smtpSender;

    /// <summary>環境別 sender を構築する。</summary>
    public NotificationSenderResolver(
        IHostEnvironment environment,
        DevelopmentNotificationSender developmentSender,
        SmtpNotificationSender smtpSender)
    {
        _environment = environment;
        _developmentSender = developmentSender;
        _smtpSender = smtpSender;
    }

    /// <summary>現在環境に応じた sender を返す。</summary>
    public INotificationSender Resolve() =>
        _environment.IsDevelopment() ? _developmentSender : _smtpSender;
}
