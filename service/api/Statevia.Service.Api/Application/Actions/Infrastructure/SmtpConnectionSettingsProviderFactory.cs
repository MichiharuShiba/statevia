using Microsoft.Extensions.Options;
using Statevia.Service.Api.Configuration;

namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary><see cref="NotificationOptions.SmtpSettingsSource"/> に応じて <see cref="ISmtpConnectionSettingsProvider"/> を選択する。</summary>
internal sealed class SmtpConnectionSettingsProviderFactory : ISmtpConnectionSettingsProvider
{
    private readonly NotificationOptions _options;
    private readonly EnvironmentSmtpConnectionSettingsProvider _environmentProvider;
    private readonly DatabaseSmtpConnectionSettingsProvider _databaseProvider;
    private readonly KmsSmtpConnectionSettingsProvider _kmsProvider;

    /// <summary>各実装を注入する。</summary>
    public SmtpConnectionSettingsProviderFactory(
        IOptions<NotificationOptions> options,
        EnvironmentSmtpConnectionSettingsProvider environmentProvider,
        DatabaseSmtpConnectionSettingsProvider databaseProvider,
        KmsSmtpConnectionSettingsProvider kmsProvider)
    {
        _options = options.Value;
        _environmentProvider = environmentProvider;
        _databaseProvider = databaseProvider;
        _kmsProvider = kmsProvider;
    }

    /// <inheritdoc />
    public Task<SmtpConnectionSettings> GetAsync(SmtpConnectionSettingsRequest request, CancellationToken cancellationToken) =>
        Resolve().GetAsync(request, cancellationToken);

    private ISmtpConnectionSettingsProvider Resolve() =>
        _options.SmtpSettingsSource switch
        {
            NotificationSmtpSettingsSource.Environment => _environmentProvider,
            NotificationSmtpSettingsSource.Database => _databaseProvider,
            NotificationSmtpSettingsSource.KeyManagementService => _kmsProvider,
            _ => throw new InvalidOperationException($"Unsupported Notification SMTP settings source: {_options.SmtpSettingsSource}."),
        };
}
