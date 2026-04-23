using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Tests.Services;

public sealed class WorkflowStreamServiceTests
{
    private sealed class FakeWorkflowService : IWorkflowService
    {
        private readonly string _graphJson;
        public int GetGraphJsonCalls { get; private set; }

        public FakeWorkflowService(string graphJson) => _graphJson = graphJson;

        public Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<WorkflowResponse>> ListPagedAsync(
            string tenantId, int offset, int limit, string? status, string? definitionId, string? nameContains, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            GetGraphJsonCalls++;
            return Task.FromResult(_graphJson);
        }
        public Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FlakyThenStableWorkflowService : IWorkflowService
    {
        private int _getGraphCalls;
        private readonly string _stableJson;

        public FlakyThenStableWorkflowService(string stableJson) => _stableJson = stableJson;

        public int GetGraphJsonCalls => _getGraphCalls;

        public Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<WorkflowResponse>> ListPagedAsync(
            string tenantId, int offset, int limit, string? status, string? definitionId, string? nameContains, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            _getGraphCalls++;
            if (_getGraphCalls == 1)
                return Task.FromException<string>(new InvalidOperationException("transient graph failure"));

            return Task.FromResult(_stableJson);
        }
        public Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>1 回目は例外、2 回目以降は JSON を返し、呼び出し間隔を記録する（SSE の catch 経路の待機時間検証用）。</summary>
    private sealed class FailOnceThenStableWithCallSpacingWorkflowService : IWorkflowService
    {
        private readonly string _stableJson;
        private int _getGraphCalls;
        private DateTime _previousCallStartedAtUtc;

        public FailOnceThenStableWithCallSpacingWorkflowService(string stableJson) => _stableJson = stableJson;

        /// <summary>2 回目以降のグラフ取得呼び出しについて、直前呼び出し開始からの経過時間。</summary>
        public TimeSpan? TimeSincePreviousGetGraphStarted { get; private set; }

        public Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct) => throw new NotSupportedException();

        public Task<PagedResult<WorkflowResponse>> ListPagedAsync(
            string tenantId, int offset, int limit, string? status, string? definitionId, string? nameContains, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            if (_getGraphCalls > 0)
                TimeSincePreviousGetGraphStarted = now - _previousCallStartedAtUtc;

            _previousCallStartedAtUtc = now;
            _getGraphCalls++;
            if (_getGraphCalls == 1)
                return Task.FromException<string>(new InvalidOperationException("transient graph failure"));

            return Task.FromResult(_stableJson);
        }

        public Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();

        public Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ThrowingWorkflowService : IWorkflowService
    {
        public Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<WorkflowResponse>> ListPagedAsync(
            string tenantId, int offset, int limit, string? status, string? definitionId, string? nameContains, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct) => throw new NotSupportedException();
        public Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct) => throw new NotSupportedException();
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
        var workflows = new ThrowingWorkflowService();
        var sut = new WorkflowStreamService(workflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        // Act
        await sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", CancellationToken.None);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
    }

    /// <summary>取消済みトークンでも配信ヘッダーを設定し本文は書き込まない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenCtAlreadyCanceled_SetsHeadersAndDoesNotWrite()
    {
        // Arrange
        var uuid = Guid.NewGuid();
        var display = new FakeDisplayIdService
        {
            ResolveResult = uuid,
            GetDisplayIdResult = "EXEC-1"
        };
        var workflows = new ThrowingWorkflowService();
        var sut = new WorkflowStreamService(workflows, display);

        var http = new DefaultHttpContext();
        var body = new MemoryStream();
        http.Response.Body = body;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", cts.Token);

        // Assert
        Assert.Equal("text/event-stream", http.Response.ContentType);
        Assert.Equal("no-cache, no-transform", http.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("keep-alive", http.Response.Headers["Connection"].ToString());
        Assert.Equal("no", http.Response.Headers["X-Accel-Buffering"].ToString());
        Assert.Equal(0, body.Length);
    }

    /// <summary>グラフ文字列が変化したとき更新イベントを一回だけ書き込む。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGraphJsonChanges_WritesGraphUpdatedOnce()
    {
        // Arrange
        var graphJson = "{\"nodes\":[]}";
        var fakeWorkflows = new FakeWorkflowService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-1"
        };

        var sut = new WorkflowStreamService(fakeWorkflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(WorkflowStreamService.GraphPollingIntervalMilliseconds - 500); // 1 周目の取得後、次の待機中にキャンセル

        // Act
        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", cts.Token));

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
        Assert.Equal(1, fakeWorkflows.GetGraphJsonCalls); // likely only first iteration
    }

    /// <summary>二周目で内容が同じなら二回目の更新イベントを書き込まない。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenHashUnchangedOnSecondIteration_DoesNotWriteSecondTime()
    {
        // Arrange
        var graphJson = "{\"nodes\":[{\"nodeId\":\"n1\",\"stateName\":null,\"startedAt\":\"2020-01-01T00:00:00Z\",\"completedAt\":null,\"fact\":null}]}";
        var fakeWorkflows = new FakeWorkflowService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-1"
        };

        var sut = new WorkflowStreamService(fakeWorkflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(WorkflowStreamService.GraphPollingIntervalMilliseconds * 2 + 200); // 2 周のポーリング後、次の待機中にキャンセル

        // Act
        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", cts.Token));

        // Assert
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        var count = bodyText.Split("GraphUpdated", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, count);
        Assert.True(fakeWorkflows.GetGraphJsonCalls >= 2);
    }

    /// <summary>表示用識別子がないとき入力識別子を実行識別子として載せる。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGetDisplayIdMissing_UsesIdOrUuidAsExecutionId()
    {
        // Arrange
        var graphJson = "{\"nodes\":[]}";
        var fakeWorkflows = new FakeWorkflowService(graphJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = null
        };

        var sut = new WorkflowStreamService(fakeWorkflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        const string idOrUuid = "client-supplied-id";
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(WorkflowStreamService.GraphPollingIntervalMilliseconds - 500);

        // Act
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            sut.WriteStreamAsync(http.Response, "t1", idOrUuid: idOrUuid, cts.Token));

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
        var fakeWorkflows = new FlakyThenStableWorkflowService(stableJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-FLAKY"
        };

        var sut = new WorkflowStreamService(fakeWorkflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(WorkflowStreamService.GraphPollingIntervalMilliseconds * 2 + 500);

        // Act
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", cts.Token));

        // Assert
        Assert.True(fakeWorkflows.GetGraphJsonCalls >= 2);
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
    }

    /// <summary>グラフ取得が例外のときも、成功時と同じポーリング間隔で再試行する。</summary>
    [Fact]
    public async Task WriteStreamAsync_WhenGetGraphJsonThrowsOnce_WaitsGraphPollingIntervalBeforeRetry()
    {
        // Arrange
        var stableJson = "{\"nodes\":[]}";
        var fakeWorkflows = new FailOnceThenStableWithCallSpacingWorkflowService(stableJson);
        var display = new FakeDisplayIdService
        {
            ResolveResult = Guid.NewGuid(),
            GetDisplayIdResult = "EXEC-TIMING"
        };

        var sut = new WorkflowStreamService(fakeWorkflows, display);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(WorkflowStreamService.GraphPollingIntervalMilliseconds * 2 + 500);

        // Act
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            sut.WriteStreamAsync(http.Response, "t1", idOrUuid: "X", cts.Token));

        // Assert
        Assert.NotNull(fakeWorkflows.TimeSincePreviousGetGraphStarted);
        Assert.True(
            fakeWorkflows.TimeSincePreviousGetGraphStarted >= TimeSpan.FromMilliseconds(WorkflowStreamService.GraphPollingIntervalMilliseconds - 150),
            $"Expected spacing >= {WorkflowStreamService.GraphPollingIntervalMilliseconds - 150} ms, was {fakeWorkflows.TimeSincePreviousGetGraphStarted!.Value.TotalMilliseconds} ms");
        var bodyText = System.Text.Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
        Assert.Contains("GraphUpdated", bodyText);
    }
}

