using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Reference.Http;

namespace Statevia.Reference.Http.Tests;

/// <summary>http.request Action の HTTPS / Idempotency-Key 検証。</summary>
public sealed class HttpRequestActionStateTests
{
    /// <summary>Idempotency-Key ヘッダを付与する。</summary>
    [Fact]
    public async Task ExecuteAsync_SendsIdempotencyKeyHeader()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var state = new HttpRequestActionState(() => CreateClient((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }));
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["idempotencyKey"] = "key-1",
        };

        // Act
        await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured.Headers.TryGetValues("Idempotency-Key", out var values));
        Assert.Equal("key-1", Assert.Single(values));
    }

    /// <summary>HTTP URL は拒否される。</summary>
    [Fact]
    public async Task ExecuteAsync_HttpUrl_Throws()
    {
        // Arrange
        var state = new HttpRequestActionState();
        var input = new Dictionary<string, object?>
        {
            ["url"] = "http://example.com/hook",
            ["method"] = "GET",
        };

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(CreateContext(), input, CancellationToken.None));
    }

    /// <summary>Module が request Action を公開する。</summary>
    [Fact]
    public void Module_ExposesRequestAction()
    {
        // Arrange
        var module = new HttpReferenceModule();
        var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var actions = module.GetActions(services).ToArray();

        // Assert
        Assert.Equal(HttpReferenceActionIds.ModuleId, module.ModuleId);
        Assert.Equal(HttpReferenceActionIds.Request, Assert.Single(actions).ActionId);
        Assert.NotNull(Assert.Single(actions).Publication);
    }

    private static StateContext CreateContext() =>
        new()
        {
            Events = new StubEventProvider(),
            Store = new StubStateStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "Call",
            Logger = NullLogger.Instance,
        };

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://example.com"),
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class StubEventProvider : IEventProvider
    {
        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Signal(string signalName)
        {
        }

        public void Resume(string nodeId, string eventName)
        {
        }

        public void PublishTopic(string topic, object? payloadSummary)
        {
        }
    }

    private sealed class StubStateStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }
}
