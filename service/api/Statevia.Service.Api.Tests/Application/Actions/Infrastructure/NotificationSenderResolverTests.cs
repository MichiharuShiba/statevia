using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Infrastructure.Notification;
using Statevia.Infrastructure.Notification.Configuration;

namespace Statevia.Service.Api.Tests.Application.Actions.Infrastructure;

/// <summary><see cref="NotificationSenderResolver"/> の環境分岐テスト。</summary>
public sealed class NotificationSenderResolverTests
{
    /// <summary>Development では DevelopmentNotificationSender を返す。</summary>
    [Fact]
    public void Resolve_Development_ReturnsDevelopmentSender()
    {
        // Arrange
        var resolver = new NotificationSenderResolver(
            new FakeHostEnvironment(Environments.Development),
            new DevelopmentNotificationSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<DevelopmentNotificationSender>.Instance),
            new SmtpNotificationSender(CreateSettingsProvider()));

        // Act
        var sender = resolver.Resolve();

        // Assert
        Assert.IsType<DevelopmentNotificationSender>(sender);
    }

    /// <summary>Production では SmtpNotificationSender を返す。</summary>
    [Fact]
    public void Resolve_Production_ReturnsSmtpSender()
    {
        // Arrange
        var resolver = new NotificationSenderResolver(
            new FakeHostEnvironment(Environments.Production),
            new DevelopmentNotificationSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<DevelopmentNotificationSender>.Instance),
            new SmtpNotificationSender(CreateSettingsProvider()));

        // Act
        var sender = resolver.Resolve();

        // Assert
        Assert.IsType<SmtpNotificationSender>(sender);
    }

    private static ISmtpConnectionSettingsProvider CreateSettingsProvider() =>
        new SmtpConnectionSettingsProviderFactory(
            Options.Create(new NotificationOptions()),
            new EnvironmentSmtpConnectionSettingsProvider(Options.Create(new NotificationOptions())),
            new DatabaseSmtpConnectionSettingsProvider(),
            new KmsSmtpConnectionSettingsProvider());
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
