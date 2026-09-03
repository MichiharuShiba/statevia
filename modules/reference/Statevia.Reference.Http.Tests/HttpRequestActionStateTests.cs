using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using System.Net;
using System.Text;

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
        var state = CreateActionState(() => CreateClient((request, _) =>
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

    /// <summary>入力がオブジェクトでないとき失敗する。</summary>
    [Fact]
    public async Task ExecuteAsync_NonObjectInput_Throws()
    {
        // Arrange
        var state = new HttpRequestActionState();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(CreateContext(), "not-an-object", CancellationToken.None));
        Assert.Contains("input.url", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>timeout・headers・JSON body を付与しレスポンスを辞書へ写す。</summary>
    [Fact]
    public async Task ExecuteAsync_SendsHeadersAndBody_AndMapsResponse()
    {
        // Arrange
        HttpClient? client = null;
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var state = CreateActionState(() =>
        {
            client = CreateClient(async (request, _) =>
            {
                captured = request;
                capturedBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
                };
                response.Headers.TryAddWithoutValidation("X-Trace", "abc");
                return response;
            });
            return client;
        });
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["timeout"] = 12,
            ["headers"] = new Dictionary<string, object?>
            {
                ["X-Custom"] = "yes",
                ["X-Skip"] = 1,
            },
            ["body"] = new Dictionary<string, object?> { ["name"] = "n1" },
        };

        // Act
        var result = await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(12), client!.Timeout);
        Assert.NotNull(captured);
        Assert.True(captured.Headers.TryGetValues("X-Custom", out var customValues));
        Assert.Equal("yes", Assert.Single(customValues));
        Assert.False(captured.Headers.Contains("X-Skip"));
        Assert.Contains("n1", capturedBody, StringComparison.Ordinal);
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(201, dict["statusCode"]);
        Assert.Equal("{\"ok\":true}", dict["body"]);
        var headers = Assert.IsType<Dictionary<string, object?>>(dict["headers"]);
        Assert.True(headers.ContainsKey("X-Trace"));
    }

    /// <summary>文字列 body を送る。</summary>
    [Fact]
    public async Task ExecuteAsync_StringBody_SendsContent()
    {
        // Arrange
        string? capturedBody = null;
        var state = CreateActionState(() => CreateClient(async (request, _) =>
        {
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }));
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["body"] = "raw-text",
        };

        // Act
        await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        Assert.Equal("raw-text", capturedBody);
    }

    /// <summary>配列 body を JSON として送る。</summary>
    [Fact]
    public async Task ExecuteAsync_ArrayBody_SendsJsonArray()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var state = CreateActionState(() => CreateClient(async (request, _) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }));
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["body"] = new object[] { 1, 2 },
        };

        // Act
        await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.StartsWith("[", capturedBody, StringComparison.Ordinal);
    }

    /// <summary>上限を超える body は拒否される。</summary>
    [Fact]
    public async Task ExecuteAsync_OversizedBody_Throws()
    {
        // Arrange
        var state = CreateActionState(() => CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "POST",
            ["body"] = new string('a', 1_048_577),
        };

        // Act / Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(CreateContext(), input, CancellationToken.None));
        Assert.Contains("maximum allowed size", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>空白の idempotencyKey はヘッダに載せない。</summary>
    [Fact]
    public async Task ExecuteAsync_WhitespaceIdempotencyKey_DoesNotAddHeader()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var state = CreateActionState(() => CreateClient((request, _) =>
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
            ["method"] = "GET",
            ["idempotencyKey"] = "   ",
        };

        // Act
        await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.False(captured.Headers.Contains("Idempotency-Key"));
    }

    /// <summary>1 MiB を超えるレスポンス body は切り詰める。</summary>
    [Fact]
    public async Task ExecuteAsync_OversizedResponseBody_Truncates()
    {
        // Arrange
        var oversized = new string('x', 1_048_577);
        var state = CreateActionState(() => CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(oversized, Encoding.UTF8, "text/plain"),
            })));
        var input = new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["method"] = "GET",
        };

        // Act
        var result = await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

        // Assert
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        var body = Assert.IsType<string>(dict["body"]);
        Assert.Equal(1_048_576, body.Length);
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
        var registration = Assert.Single(actions);
        _ = registration.ExecutorFactory(services);

        // Assert
        Assert.Equal(HttpReferenceActionIds.ModuleId, module.ModuleId);
        Assert.Equal(HttpReferenceActionIds.Request, registration.ActionId);
        Assert.NotNull(registration.Publication);
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

    private static HttpRequestActionState CreateActionState(Func<HttpClient> httpClientFactory) =>
        new(httpClientFactory, static (_, _) =>
            Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") }));

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
