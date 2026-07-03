using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Statevia.Infrastructure.Notification.DependencyInjection;

/// <summary>通知送信インフラの DI 登録。</summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// SMTP 接続設定プロバイダと <see cref="INotificationSender"/> 実装を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    public static IServiceCollection AddStateviaInfrastructureNotification(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .PostConfigure(ApplyNotificationOptionsEnvironmentOverrides);

        services.AddSingleton<EnvironmentSmtpConnectionSettingsProvider>();
        services.AddSingleton<DatabaseSmtpConnectionSettingsProvider>();
        services.AddSingleton<KmsSmtpConnectionSettingsProvider>();
        services.AddSingleton<SmtpConnectionSettingsProviderFactory>();
        services.AddSingleton<ISmtpConnectionSettingsProvider>(sp =>
            sp.GetRequiredService<SmtpConnectionSettingsProviderFactory>());
        services.AddSingleton<DevelopmentNotificationSender>();
        services.AddSingleton<SmtpNotificationSender>();
        services.AddSingleton<NotificationSenderResolver>();

        return services;
    }

    private static void ApplyNotificationOptionsEnvironmentOverrides(NotificationOptions options)
    {
        var source = Environment.GetEnvironmentVariable(NotificationOptions.SmtpSourceEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (string.Equals(source, "Kms", StringComparison.OrdinalIgnoreCase))
        {
            options.SmtpSettingsSource = NotificationSmtpSettingsSource.KeyManagementService;
            return;
        }

        if (Enum.TryParse(source, ignoreCase: true, out NotificationSmtpSettingsSource parsed))
        {
            options.SmtpSettingsSource = parsed;
        }
    }
}
