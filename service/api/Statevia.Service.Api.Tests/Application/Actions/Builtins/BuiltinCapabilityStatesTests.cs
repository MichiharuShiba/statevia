using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Infrastructure;
using Statevia.Service.Api.Configuration;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary>sleep / signal / publish / rest / notification / workflow Builtin の単体テスト。</summary>
public sealed class BuiltinCapabilityStatesTests
{
    private sealed class FakeEventProvider : IEventProvider
    {
        public string? LastSignal { get; private set; }
        public string? LastTopic { get; private set; }

        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public void Signal(string signalName) => LastSignal = signalName;

        public void PublishTopic(string topic, object? payloadSummary) => LastTopic = topic;
    }

    private sealed class FakeStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }

    private static StateContext MakeContext(IEventProvider events) =>
        new()
        {
            Events = events,
            Store = new FakeStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "S1",
        };

    /// <summary>sleep は duration 後に Unit を返す。</summary>
    [Fact]
    public async Task SleepActionState_WithDuration_ReturnsUnit()
    {
        // Arrange
        var state = new SleepActionState();
        var input = new Dictionary<string, object?> { ["duration"] = "20ms" };

        // Act
        var result = await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
    }

    /// <summary>signal は指定シグナルを発行する。</summary>
    [Fact]
    public async Task SignalActionState_PublishesSignal()
    {
        // Arrange
        var events = new FakeEventProvider();
        var state = new SignalActionState();
        var input = new Dictionary<string, object?> { ["signal"] = "approval" };

        // Act
        await state.ExecuteAsync(MakeContext(events), input, CancellationToken.None);

        // Assert
        Assert.Equal("approval", events.LastSignal);
    }

    /// <summary>publish は topic dispatch を記録する。</summary>
    [Fact]
    public async Task PublishActionState_DispatchesTopic()
    {
        // Arrange
        var events = new FakeEventProvider();
        var state = new PublishActionState();
        var input = new Dictionary<string, object?> { ["topic"] = "payment.completed" };

        // Act
        var result = await state.ExecuteAsync(MakeContext(events), input, CancellationToken.None);

        // Assert
        Assert.Equal("payment.completed", events.LastTopic);
        Assert.IsType<Dictionary<string, object?>>(result);
    }

    /// <summary>rest は idempotencyKey ヘッダを付与する。</summary>
    [Fact]
    public async Task RestActionState_SendsIdempotencyKeyHeader()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(_ => new StubHttpClientFactory((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var state = new RestActionState(scopeFactory);
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["idempotencyKey"] = "key-1",
        };

        // Act
        await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured.Headers.TryGetValues("Idempotency-Key", out var values));
        Assert.Equal("key-1", Assert.Single(values));
    }

    /// <summary>rest は非 HTTPS URL を拒否する。</summary>
    [Fact]
    public async Task RestActionState_HttpUrl_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(_ => new StubHttpClientFactory((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var provider = services.BuildServiceProvider();
        var state = new RestActionState(provider.GetRequiredService<IServiceScopeFactory>());
        var input = new Dictionary<string, object?>
        {
            ["url"] = "http://example.com/hook",
            ["method"] = "GET",
        };

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None));
    }

    /// <summary>signal は current 以外の target を拒否する。</summary>
    [Fact]
    public async Task SignalActionState_InvalidTarget_Throws()
    {
        // Arrange
        var state = new SignalActionState();
        var input = new Dictionary<string, object?>
        {
            ["signal"] = "approval",
            ["target"] = "other",
        };

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None));
    }

    /// <summary>rest は JSON body とカスタムヘッダを送信する。</summary>
    [Fact]
    public async Task RestActionState_SendsBodyAndHeaders()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(_ => new StubHttpClientFactory((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
            });
        }));
        var provider = services.BuildServiceProvider();
        var state = new RestActionState(provider.GetRequiredService<IServiceScopeFactory>());
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["headers"] = new Dictionary<string, object?> { ["X-Test"] = "1" },
            ["body"] = new Dictionary<string, object?> { ["key"] = "value" },
        };

        // Act
        var result = await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.NotNull(captured.Content);
        Assert.True(captured.Headers.TryGetValues("X-Test", out _));
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(201, dict["statusCode"]);
    }

    /// <summary>notification は Development で no-op 送信結果を返す。</summary>
    [Fact]
    public async Task NotificationActionState_Development_ReturnsSkippedResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment { EnvironmentName = Environments.Development });
        services.AddLogging();
        services.AddSingleton<ISmtpConnectionSettingsProvider>(sp =>
            sp.GetRequiredService<SmtpConnectionSettingsProviderFactory>());
        services.AddSingleton<EnvironmentSmtpConnectionSettingsProvider>();
        services.AddSingleton<DatabaseSmtpConnectionSettingsProvider>();
        services.AddSingleton<KmsSmtpConnectionSettingsProvider>();
        services.AddSingleton<SmtpConnectionSettingsProviderFactory>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new NotificationOptions()));
        services.AddSingleton<DevelopmentNotificationSender>();
        services.AddSingleton<SmtpNotificationSender>();
        services.AddSingleton<NotificationSenderResolver>();
        
        var provider = services.BuildServiceProvider();
        var state = new NotificationActionState(provider.GetRequiredService<IServiceScopeFactory>());
        var input = new Dictionary<string, object?>
        {
            ["channel"] = "email",
            ["to"] = "user@example.com",
            ["subject"] = "hello",
            ["body"] = "world",
        };

        // Act
        var result = await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal("email", dict["channel"]);
        Assert.Equal("development-skipped", dict["messageId"]);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        public HttpClient CreateClient(string name) =>
            new(new StubHttpMessageHandler(_handler)) { BaseAddress = new Uri("https://example.com") };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
