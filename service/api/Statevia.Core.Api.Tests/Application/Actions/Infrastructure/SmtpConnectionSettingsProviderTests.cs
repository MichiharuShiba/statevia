using Microsoft.Extensions.Options;
using Statevia.Core.Api.Application.Actions.Infrastructure;
using Statevia.Core.Api.Configuration;

namespace Statevia.Core.Api.Tests.Application.Actions.Infrastructure;

/// <summary><see cref="EnvironmentSmtpConnectionSettingsProvider"/> の解決テスト。</summary>
public sealed class EnvironmentSmtpConnectionSettingsProviderTests
{
    /// <summary>appsettings の Smtp セクションから接続設定を解決する。</summary>
    [Fact]
    public async Task GetAsync_UsesOptionsWhenEnvironmentVariablesMissing()
    {
        // Arrange
        var provider = new EnvironmentSmtpConnectionSettingsProvider(
            Options.Create(new NotificationOptions
            {
                Smtp = new SmtpConnectionOptions
                {
                    Host = "smtp.example.com",
                    Port = 465,
                    User = "user",
                    Password = "secret",
                    DefaultFrom = "noreply@example.com",
                },
            }));

        // Act
        var settings = await provider.GetAsync(new SmtpConnectionSettingsRequest(), CancellationToken.None);

        // Assert
        Assert.Equal("smtp.example.com", settings.Host);
        Assert.Equal(465, settings.Port);
        Assert.Equal("user", settings.User);
        Assert.Equal("secret", settings.Password);
        Assert.Equal("noreply@example.com", settings.DefaultFrom);
    }

    /// <summary>環境変数 STATEVIA_SMTP_* が options より優先される。</summary>
    [Fact]
    public async Task GetAsync_EnvironmentVariablesOverrideOptions()
    {
        // Arrange
        Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", "env-host");
        Environment.SetEnvironmentVariable("STATEVIA_SMTP_PORT", "2525");
        try
        {
            var provider = new EnvironmentSmtpConnectionSettingsProvider(
                Options.Create(new NotificationOptions
                {
                    Smtp = new SmtpConnectionOptions { Host = "options-host", Port = 587 },
                }));

            // Act
            var settings = await provider.GetAsync(new SmtpConnectionSettingsRequest(), CancellationToken.None);

            // Assert
            Assert.Equal("env-host", settings.Host);
            Assert.Equal(2525, settings.Port);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", null);
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_PORT", null);
        }
    }

    /// <summary>ホスト未設定時は InvalidOperationException。</summary>
    [Fact]
    public async Task GetAsync_MissingHost_Throws()
    {
        // Arrange
        var provider = new EnvironmentSmtpConnectionSettingsProvider(Options.Create(new NotificationOptions()));

        // Act
        var act = () => provider.GetAsync(new SmtpConnectionSettingsRequest(), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }
}

/// <summary><see cref="SmtpConnectionSettingsProviderFactory"/> のソース切替テスト。</summary>
public sealed class SmtpConnectionSettingsProviderFactoryTests
{
    /// <summary>Database ソース選択時は未実装例外。</summary>
    [Fact]
    public async Task GetAsync_DatabaseSource_ThrowsNotImplemented()
    {
        // Arrange
        var factory = CreateFactory(NotificationSmtpSettingsSource.Database);

        // Act
        var act = () => factory.GetAsync(new SmtpConnectionSettingsRequest(), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("Database", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>KMS ソース選択時は未実装例外。</summary>
    [Fact]
    public async Task GetAsync_KmsSource_ThrowsNotImplemented()
    {
        // Arrange
        var factory = CreateFactory(NotificationSmtpSettingsSource.KeyManagementService);

        // Act
        var act = () => factory.GetAsync(new SmtpConnectionSettingsRequest(), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("KeyManagementService", ex.Message, StringComparison.Ordinal);
    }

    private static SmtpConnectionSettingsProviderFactory CreateFactory(NotificationSmtpSettingsSource source) =>
        new(
            Options.Create(new NotificationOptions { SmtpSettingsSource = source }),
            new EnvironmentSmtpConnectionSettingsProvider(Options.Create(new NotificationOptions())),
            new DatabaseSmtpConnectionSettingsProvider(),
            new KmsSmtpConnectionSettingsProvider());
}
