using Microsoft.AspNetCore.Http;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Tests.Services;

public sealed class ExecutionStreamServiceTests
{
    private sealed class FakeExecutionService : IExecutionService
    {
        private readonly string _graphJson;
        private readonly string _status;
        public int GetGraphJsonCalls { get; private set; }

        public FakeExecutionService(string graphJson, string status = "Running")
        {
            _graphJson = graphJson;
            _status = status;
        }

        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(new ExecutionResponse { Status = _status });
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
        {
            GetGraphJsonCalls++;
            return Task.FromResult(_graphJson);
        }
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct)
        {
            GetGraphJsonCalls++;
            return Task.FromResult<string?>(_graphJson);
        }
        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FlakyThenStableExecutionService : IExecutionService
    {
        private int _getGraphCalls;
        private readonly string _stableJson;

        public FlakyThenStableExecutionService(string stableJson) => _stableJson = stableJson;

        public int GetGraphJsonCalls => _getGraphCalls;

        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(new ExecutionResponse { Status = "Running" });
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
        {
            return Task.FromResult(_stableJson);
        }
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct)
        {
            _getGraphCalls++;
            if (_getGraphCalls == 1)
                return Task.FromException<string?>(new InvalidOperationException("transient graph failure"));

            return Task.FromResult<string?>(_stableJson);
        }
        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>1 回目は例外、2 回目以降は JSON を返し、呼び出し間隔を記録する（SSE の catch 経路の待機時間検証用）。</summary>
    private sealed class FailOnceThenStableWithCallSpacingExecutionService : IExecutionService
    {
        private readonly string _stableJson;
        private int _getGraphCalls;
        private DateTime _previousCallStartedAtUtc;

        public FailOnceThenStableWithCallSpacingExecutionService(string stableJson) => _stableJson = stableJson;

        /// <summary>2 回目以降のグラフ取得呼び出しについて、直前呼び出し開始からの経過時間。</summary>
        public TimeSpan? TimeSincePreviousGetGraphStarted { get; private set; }

        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(new ExecutionResponse { Status = "Running" });
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
        {
            return Task.FromResult(_stableJson);
        }
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            if (_getGraphCalls > 0)
                TimeSincePreviousGetGraphStarted = now - _previousCallStartedAtUtc;

            _previousCallStartedAtUtc = now;
            _getGraphCalls++;
            if (_getGraphCalls == 1)
                return Task.FromException<string?>(new InvalidOperationException("transient graph failure"));

            return Task.FromResult<string?>(_stableJson);
        }

        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ThrowingExecutionService : IExecutionService
    {
        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(new ExecutionResponse { Status = "Running" });
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) => Task.FromResult("{\"nodes\":[]}");
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>開始時の存在確認は成功し、その後 snapshot 取得が null を返すテスト用実装。</summary>
    private sealed class SnapshotMissingExecutionService : IExecutionService
    {
        public int TryGetSnapshotCalls { get; private set; }

        public Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct) =>
            Task.FromResult(new ExecutionResponse { Status = "Running" });
        public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct) => Task.FromResult("{\"nodes\":[]}");
        public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct)
        {
            TryGetSnapshotCalls++;
            return Task.FromResult<string?>(null);
        }
        public Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeDisplayIdService : IDisplayIdService
    {
        public Guid? ResolveResult { get; set; }
        public string? GetDisplayIdResult { get; set; }

        public Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            Task.FromResult(ResolveResult);

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            Task.FromResult(GetDisplayIdResult);

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>識別子解決に失敗したとき未検出応答を返して配信ヘッダーを付けない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenResolveReturnsNull_Sets404AndReturns()
    {
        // Arrange
        var display = new FakeDisplayIdService { ResolveResult = null };
        var executions = new ThrowingExecutionService();
        var sut = new ExecutionStreamService(executions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
    }

    /// <summary>取消済みトークンなら即時終了し、配信ヘッダー/本文を書き込まない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenCtAlreadyCanceled_ReturnsWithoutHeadersAndBody()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService
        {
            ResolveResult = uuid,
            GetDisplayIdResult = "EXEC-1"
        };
        var executions = new ThrowingExecutionService();
        var sut = new ExecutionStreamService(executions, display);

        var http = new DefaultHttpContext();
        var body = new MemoryStream();
        http.Response.Body = body;

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", cts.Token);

        // Assert
        Assert.Null(http.Response.ContentType);
        Assert.Equal(string.Empty, http.Response.Headers["Cache-Control"].ToString());
        Assert.Equal(string.Empty, http.Response.Headers["Connection"].ToString());
        Assert.Equal(string.Empty, http.Response.Headers["X-Accel-Buffering"].ToString());
        Assert.Equal(0, body.Length);
    }

    /// <summary>グラフ文字列が変化したとき更新イベントを一回だけ書き込む。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGraphJsonChanges_WritesGraphUpdatedOnce()
    {
        // Arrange
        var graphJson = "{\"nodes\":[]}";
        var fakeExecutions = new FakeExecutionService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-1"
        };

        var sut = new ExecutionStreamService(fakeExecutions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(ExecutionStreamService.GraphPollingIntervalMilliseconds - 500); // 1 周目の取得後、次の待機中にキャンセル

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", cts.Token);

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
        Assert.True(fakeExecutions.GetGraphJsonCalls >= 1); // 1 周以上の snapshot 取得
    }

    /// <summary>二周目で内容が同じなら二回目の更新イベントを書き込まない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenHashUnchangedOnSecondIteration_DoesNotWriteSecondTime()
    {
        // Arrange
        var graphJson = "{\"nodes\":[{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}";
        var fakeExecutions = new FakeExecutionService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-1"
        };

        var sut = new ExecutionStreamService(fakeExecutions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(ExecutionStreamService.GraphPollingIntervalMilliseconds * 2 + 200); // 2 周のポーリング後、次の待機中にキャンセル

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", cts.Token);

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        var count = bodyText.Split("GraphUpdated", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, count);
        Assert.True(fakeExecutions.GetGraphJsonCalls >= 2);
    }

    /// <summary>表示用識別子がないとき入力識別子を実行識別子として載せる。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGetDisplayIdMissing_UsesIdOrUuidAsExecutionId()
    {
        // Arrange
        var graphJson = "{\"nodes\":[]}";
        var fakeExecutions = new FakeExecutionService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = null
        };

        var sut = new ExecutionStreamService(fakeExecutions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        const string idOrUuid = "client-supplied-id";
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(ExecutionStreamService.GraphPollingIntervalMilliseconds - 500);

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: idOrUuid, cts.Token);

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains(idOrUuid, bodyText);
        Assert.Contains("GraphUpdated", bodyText);
    }

    /// <summary>グラフ取得が一度失敗しても再試行後に更新イベントを書き込む。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGetGraphJsonFailsOnce_ThenRecovers_AndWritesGraphUpdated()
    {
        // Arrange
        var stableJson = "{\"nodes\":[]}";
        var fakeExecutions = new FlakyThenStableExecutionService(stableJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-FLAKY"
        };

        var sut = new ExecutionStreamService(fakeExecutions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(ExecutionStreamService.GraphPollingIntervalMilliseconds * 2 + 500);

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", cts.Token);

        // Assert
        Assert.True(fakeExecutions.GetGraphJsonCalls >= 2);
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
    }

    /// <summary>グラフ取得が例外のときも、成功時と同じポーリング間隔で再試行する。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGetGraphJsonThrowsOnce_WaitsGraphPollingIntervalBeforeRetry()
    {
        // Arrange
        var stableJson = "{\"nodes\":[]}";
        var fakeExecutions = new FailOnceThenStableWithCallSpacingExecutionService(stableJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-TIMING"
        };

        var sut = new ExecutionStreamService(fakeExecutions, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(ExecutionStreamService.GraphPollingIntervalMilliseconds * 2 + 500);

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", cts.Token);

        // Assert
        Assert.NotNull(fakeExecutions.TimeSincePreviousGetGraphStarted);
        Assert.True(
            fakeExecutions.TimeSincePreviousGetGraphStarted >= TimeSpan.FromMilliseconds(ExecutionStreamService.GraphPollingIntervalMilliseconds - 150),
            $"Expected spacing >= {ExecutionStreamService.GraphPollingIntervalMilliseconds - 150} ms, was {fakeExecutions.TimeSincePreviousGetGraphStarted!.Value.TotalMilliseconds} ms");
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
    }

    /// <summary>snapshot が見つからない場合は配信を終了し、本文を書き込まない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenSnapshotMissing_EndsStreamWithoutWriting()
    {
        // Arrange
        var executions = new SnapshotMissingExecutionService();
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-1"
        };
        var sut = new ExecutionStreamService(executions, display);
        var http = new DefaultHttpContext();
        var body = new MemoryStream();
        http.Response.Body = body;

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal("text/event-stream", http.Response.ContentType);
        Assert.Equal(1, executions.TryGetSnapshotCalls);
        Assert.Equal(0, body.Length);
    }

    /// <summary>スナップショットが終端を示したら、次ポーリングを待たずにストリームを終了する。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenExecutionIsTerminal_EndsStreamImmediatelyAfterFirstUpdate()
    {
        // Arrange
        var graphJson = """
                        {
                          "nodes": [
                            {
                              "nodeId": "start",
                              "completedAt": "2020-01-01T00:00:00Z",
                              "fact": "Completed"
                            },
                            {
                              "nodeId": "end",
                              "completedAt": "2020-01-01T00:00:01Z",
                              "fact": "Completed"
                            }
                          ],
                          "edges": [
                            {
                              "from": "start",
                              "to": "end"
                            }
                          ]
                        }
                        """;
        var executions = new FakeExecutionService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-TERMINAL"
        };
        var sut = new ExecutionStreamService(executions, display);

        var http = new DefaultHttpContext();
        var body = new MemoryStream();
        http.Response.Body = body;

        // Act
        await sut.WriteStreamAsync(http.Response, idOrUuid: "X", CancellationToken.None);

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("GraphUpdated", bodyText);
        Assert.Equal(1, executions.GetGraphJsonCalls);
    }
}

