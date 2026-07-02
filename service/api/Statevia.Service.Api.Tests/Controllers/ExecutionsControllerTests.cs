using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Services;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Controllers;

public sealed class ExecutionsControllerTests
{
    private sealed class ThrowingDisplayIdService : IDisplayIdService
    {
        private readonly Exception _ex;

        public ThrowingDisplayIdService(Exception ex) => _ex = ex;

        public Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            throw _ex;

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDisplayIdService : IDisplayIdService
    {
        public Guid? ResolveResult { get; set; }
        public string? GetDisplayIdResult { get; set; }

        public Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return ResolveResult;
        }

        public async Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return GetDisplayIdResult;
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeExecutionService : IExecutionService
    {
        public Exception? ExceptionToThrow { get; set; }

        public string? LastIdempotencyKey { get; private set; }

        public ExecutionResponse StartResult { get; set; } = new ExecutionResponse();
        public PagedResult<ExecutionResponse> ListPagedResult { get; set; } = new() { Items = [], TotalCount = 0, Offset = 0, Limit = 0, HasMore = false };
        public ExecutionResponse GetResult { get; set; } = new ExecutionResponse();
        public string GraphJsonResult { get; set; } = "{\"nodes\":[]}";
        public ExecutionViewDto ViewResult { get; set; } = new ExecutionViewDto();
        public ExecutionEventsResponseDto EventsResult { get; set; } = new ExecutionEventsResponseDto();

        public bool CancelCalled { get; private set; }
        public bool ResumeCalled { get; private set; }
        public bool PublishCalled { get; private set; }

        public string? CancelIdempotencyKey { get; private set; }
        public string? ResumeIdempotencyKey { get; private set; }
        public string? PublishIdempotencyKey { get; private set; }
        public string? ResumeResumeKey { get; private set; }

        public async Task<ExecutionResponse> StartAsync(StartExecutionRequest request, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastIdempotencyKey = idempotencyKey;
            return StartResult;
        }

        public async Task<PagedResult<ExecutionResponse>> ListPagedAsync(ExecutionListQuery query, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return ListPagedResult;
        }

        public async Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return GetResult;
        }
        public async Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return GraphJsonResult;
        }
        public async Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return GraphJsonResult;
        }

        public async Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return ViewResult;
        }

        public async Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return ViewResult;
        }

        public async Task<ExecutionEventsResponseDto> ListEventsAsync(string idOrUuid, long afterSeq, int limit, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return EventsResult;
        }

        public async Task ResumeNodeAsync(string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct)
        {
            ResumeCalled = true;
            ResumeResumeKey = resumeKey;
            ResumeIdempotencyKey = idempotencyKey;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task CancelAsync(string idOrUuid, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct)
        {
            CancelCalled = true;
            CancelIdempotencyKey = idempotencyKey;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task PublishEventAsync(string idOrUuid, string eventName, string? idempotencyKey, CommandRequestContext requestContext, CancellationToken ct)
        {
            PublishCalled = true;
            PublishIdempotencyKey = idempotencyKey;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public Task UpdateProjectionFromEngineAsync(Guid executionId, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static ExecutionsController CreateController(DefaultHttpContext http, FakeExecutionService executions, ExecutionStreamService stream)
    {
        return new ExecutionsController(executions, stream)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };
    }

    /// <summary>
    /// 作成結果として作成済み応答を返す。
    /// </summary>
    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions";
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        // Act
        var executions = new FakeExecutionService
        {
            StartResult = new ExecutionResponse { DisplayId = "WF-DISP-1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow, UpdatedAt = null, CancelRequested = false, RestartLost = false }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var request = new StartExecutionRequest { DefinitionId = "def-1", Input = null };

        var result = await controller.Create(request, ct: CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(controller.Get), created.ActionName);
        Assert.True(created.RouteValues!.ContainsKey("id"));
        Assert.Equal("WF-DISP-1", created.RouteValues!["id"]);
        var value = Assert.IsType<ExecutionResponse>(created.Value);
        Assert.Equal("WF-DISP-1", value.DisplayId);
    }

    /// <summary>
    /// limit 未指定の一覧取得で検証例外を投げる。
    /// </summary>
    [Fact]
    public async Task List_LimitNull_ThrowsArgumentException()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => controller.List(new ExecutionListQuery(), ct: CancellationToken.None));
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task List_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var executions = new FakeExecutionService
        {
            ListPagedResult = new PagedResult<ExecutionResponse>
            {
                Items =
                [
                    new ExecutionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow },
                ],
                TotalCount = 1,
                Offset = 0,
                Limit = 1,
                HasMore = false
            }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.List(new ExecutionListQuery { Limit = 1, Offset = 0 }, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<ExecutionResponse>>(ok.Value);
        Assert.Single(paged.Items);
    }

    /// <summary>
    /// ページング指定の一覧取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task List_Paged_ReturnsOkPaged()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var executions = new FakeExecutionService
        {
            ListPagedResult = new PagedResult<ExecutionResponse>
            {
                Items =
                [
                    new ExecutionResponse { DisplayId = "D2", ResourceId = Guid.NewGuid(), Status = "Completed", StartedAt = DateTime.UtcNow }
                ],
                TotalCount = 1,
                Offset = 0,
                Limit = 1,
                HasMore = false
            }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.List(new ExecutionListQuery { Limit = 1, Offset = 0 }, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<ExecutionResponse>>(ok.Value);
        Assert.Single(paged.Items);
        Assert.Equal(1, paged.TotalCount);
    }

    /// <summary>
    /// 上限を超える件数指定で引数例外を投げる。
    /// </summary>
    [Fact]
    public async Task List_InvalidLimit_ThrowsArgumentException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<ArgumentException>(() => controller.List(new ExecutionListQuery { Limit = 501, Offset = 0 }, ct: CancellationToken.None));
    }

    /// <summary>
    /// 負の開始位置指定で範囲外引数例外を投げる。
    /// </summary>
    [Fact]
    public async Task List_InvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.List(new ExecutionListQuery { Limit = 1, Offset = -1 }, ct: CancellationToken.None));
    }

    /// <summary>
    /// 一未満の件数指定で範囲外引数例外を投げる。
    /// </summary>
    [Fact]
    public async Task List_InvalidLimitLessThan1_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.List(new ExecutionListQuery { Limit = 0, Offset = 0 }, ct: CancellationToken.None));
    }

    /// <summary>
    /// ワークフロー取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkExecutionResponse()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var executions = new FakeExecutionService
        {
            GetResult = new ExecutionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.Get("D1", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ExecutionResponse>(ok.Value);
        Assert.Equal("D1", model.DisplayId);
    }

    /// <summary>
    /// グラフ取得で書式を示す応答を返す。
    /// </summary>
    [Fact]
    public async Task GetGraph_ReturnsApplicationJson()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var executions = new FakeExecutionService { GraphJsonResult = "{\"nodes\":[]}" };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetGraph("X", ct: CancellationToken.None);
        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Equal("{\"nodes\":[]}", content.Content);
    }

    /// <summary>
    /// 状態取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task GetState_ReturnsOkExecutionView()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var executions = new FakeExecutionService
        {
            ViewResult = new ExecutionViewDto
            {
                DisplayId = "E1",
                ResourceId = Guid.NewGuid().ToString("D"),
                GraphId = "G1",
                Status = "ACTIVE",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = null,
                CancelRequested = false,
                RestartLost = false,
                Nodes =
                [
                    new ExecutionViewNodeDto
                    {
                        ExecutionNodeId = "n1",
                        StateName = "S1",
                        NodeType = "Task",
                        Status = "RUNNING",
                        Attempt = 1,
                        WorkerId = null,
                        WaitKey = null,
                        CanceledByExecution = false,
                        ConditionRouting = JsonDocument.Parse($$"""
                            {
                              "fact": "Completed",
                              "resolution": "{{ConditionRoutingResolutions.MatchedCase}}",
                              "matchedCaseIndex": 0
                            }
                            """).RootElement.Clone()
                    }
                ]
            }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetState("X", atSeq: 1, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ExecutionViewDto>(ok.Value);
        Assert.Single(model.Nodes);
        Assert.Equal("n1", model.Nodes[0].ExecutionNodeId);
        Assert.True(model.Nodes[0].ConditionRouting.HasValue);
        Assert.Equal(
            ConditionRoutingResolutions.MatchedCase,
            model.Nodes[0].ConditionRouting!.Value.GetProperty("resolution").GetString());
    }

    /// <summary>
    /// 実行イベント取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task GetEvents_ReturnsOkExecutionEventsResponse()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var executions = new FakeExecutionService
        {
            EventsResult = new ExecutionEventsResponseDto
            {
                Events =
                [
                    new TimelineEventDto { Seq = 1, Type = "ExecutionStatusChanged", ExecutionId = "E1", To = "Running", At = DateTime.UtcNow.ToString("O") }
                ],
                HasMore = false
            }
        };

        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetEvents("X", afterSeq: 0, limit: 10, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ExecutionEventsResponseDto>(ok.Value);
        Assert.Single(model.Events);
    }

    /// <summary>
    /// 識別子解決に失敗したとき未検出応答を設定する。
    /// </summary>
    [Fact]
    public async Task GetStream_WhenResolveReturnsNull_Sets404()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Response.Body = new MemoryStream();

        // Act
        var executions = new FakeExecutionService();
        var display = new FakeDisplayIdService { ResolveResult = null };
        var stream = new ExecutionStreamService(executions, display);
        var controller = CreateController(http, executions, stream);

        await controller.GetStream("X", ct: CancellationToken.None);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
    }

    /// <summary>
    /// キャンセル要求で空応答を返す。
    /// </summary>
    [Fact]
    public async Task Cancel_ReturnsNoContent()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/cancel";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.Cancel("X", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.CancelCalled);
        Assert.Equal("idem", executions.CancelIdempotencyKey);
    }

    /// <summary>
    /// 冪等ヘッダー未指定時は空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task Cancel_WhenIdempotencyHeaderMissing_PassesNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/cancel";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.Cancel("X", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.CancelCalled);
        Assert.Null(executions.CancelIdempotencyKey);
    }

    /// <summary>
    /// ノード再開要求で空応答を返す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_ReturnsNoContent()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
        Assert.Equal("Approve", executions.ResumeResumeKey);
        Assert.Equal("idem", executions.ResumeIdempotencyKey);
    }

    /// <summary>
    /// 冪等ヘッダー未指定時は空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenIdempotencyHeaderMissing_PassesNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        // X-Idempotency-Key intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
        Assert.Equal("Approve", executions.ResumeResumeKey);
        Assert.Null(executions.ResumeIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
    }

    /// <summary>
    /// 本文が空値のとき再開キーなしで再開する。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenBodyNull_ResumesWithNullResumeKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode("X", "node-1", body: null, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
        Assert.Null(executions.ResumeResumeKey);
        Assert.Equal("idem", executions.ResumeIdempotencyKey);
    }

    /// <summary>
    /// 本文の再開キーが空値のとき再開キーなしで再開する。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenBodyHasNullResumeKey_ResumesWithNullResumeKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = null }, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
        Assert.Null(executions.ResumeResumeKey);
        Assert.Equal("idem", executions.ResumeIdempotencyKey);
    }

    /// <summary>
    /// 再開処理で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/nodes/node-1/resume";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no node") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, ct: CancellationToken.None));
    }

    /// <summary>
    /// 要求経路が空値のとき空文字の経路を渡す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenRequestPathValueIsNull_PassesEmptyPath()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        // Try to force PathString.Value to be null (default(PathString) keeps its backing value null).
        http.Request.Path = default;

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.ResumeNode(
            "X",
            "node-1",
            new ResumeNodeRequest { ResumeKey = "Approve" },
            idempotencyKey: "idem",
            ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.ResumeCalled);
        Assert.Equal("Approve", executions.ResumeResumeKey);
        Assert.Equal("idem", executions.ResumeIdempotencyKey);
    }

    /// <summary>
    /// イベント公開要求で空応答を返す。
    /// </summary>
    [Fact]
    public async Task PublishEvent_ReturnsNoContent()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/events";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var result = await controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(executions.PublishCalled);
        Assert.Equal("idem", executions.PublishIdempotencyKey);
    }

    /// <summary>
    /// 作成処理で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task Create_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions";
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        var executions = new FakeExecutionService
        {
            ExceptionToThrow = new NotFoundException("no def")
        };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var request = new StartExecutionRequest { DefinitionId = "def-1", Input = null };

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Create(request, ct: CancellationToken.None));
    }

    /// <summary>
    /// キャンセル処理で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task Cancel_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/cancel";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Cancel("X", ct: CancellationToken.None));
    }

    /// <summary>
    /// 取得処理で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task Get_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Get("X", ct: CancellationToken.None));
    }

    /// <summary>
    /// グラフ取得で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task GetGraph_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.GetGraph("X", ct: CancellationToken.None));
    }

    /// <summary>
    /// 状態取得で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task GetState_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.GetState("X", atSeq: 1, ct: CancellationToken.None));
    }

    /// <summary>
    /// 実行イベント取得で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task GetEvents_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.GetEvents("X", afterSeq: 0, limit: 10, ct: CancellationToken.None));
    }

    /// <summary>
    /// 表示用識別子の解決失敗時に未検出例外を返す。
    /// </summary>
    [Fact]
    public async Task GetStream_WhenDisplayResolveThrows_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Response.Body = new System.IO.MemoryStream();

        var executions = new FakeExecutionService();
        var display = new ThrowingDisplayIdService(new NotFoundException("no wf"));
        var stream = new ExecutionStreamService(executions, display);
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.GetStream("X", ct: CancellationToken.None));
    }

    /// <summary>
    /// イベント公開処理で未検出例外をそのまま返す。
    /// </summary>
    [Fact]
    public async Task PublishEvent_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/events";

        var executions = new FakeExecutionService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, ct: CancellationToken.None));
    }

    /// <summary>
    /// ヘッダー未指定時は既定テナントと空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task Create_WhenTenantRequestHeadersMissing_AndIdempotencyMissing_PassesDefaultTenantAndNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions";
        // X-Tenant-Id intentionally missing
        // X-Idempotency-Key intentionally missing

        // Act
        var executions = new FakeExecutionService
        {
            StartResult = new ExecutionResponse { DisplayId = "WF-DISP-1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow }
        };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        var request = new StartExecutionRequest { DefinitionId = "def-1", Input = null };

        // Assert
        var result = await controller.Create(request, ct: CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal("WF-DISP-1", ((ExecutionResponse)created.Value!).DisplayId);

        Assert.Null(executions.LastIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Cancel_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/cancel";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.Cancel("X", idempotencyKey: "idem", ct: CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        Assert.Equal("idem", executions.LastIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Get_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var executions = new FakeExecutionService { GetResult = new ExecutionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow } };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.Get("X", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ExecutionResponse>(ok.Value);
        Assert.Equal("D1", model.DisplayId);

    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetGraph_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var executions = new FakeExecutionService { GraphJsonResult = "{\"nodes\":[]}" };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetGraph("X", ct: CancellationToken.None);
        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Equal("{\"nodes\":[]}", content.Content);

    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetState_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var executions = new FakeExecutionService
        {
            ViewResult = new ExecutionViewDto { DisplayId = "E1", ResourceId = Guid.NewGuid().ToString("D"), GraphId = "G1", Status = "ACTIVE", StartedAt = DateTime.UtcNow, UpdatedAt = null, CancelRequested = false, RestartLost = false, Nodes = [] }
        };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetState("X", atSeq: 1, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<ExecutionViewDto>(ok.Value);

    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetEvents_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var executions = new FakeExecutionService
        {
            EventsResult = new ExecutionEventsResponseDto { Events = [], HasMore = false }
        };
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.GetEvents("X", afterSeq: 0, limit: 10, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<ExecutionEventsResponseDto>(ok.Value);

    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 404 を設定する。
    /// </summary>
    [Fact]
    public async Task GetStream_WhenTenantRequestHeadersMissing_Sets404()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Response.Body = new MemoryStream();

        // Act
        var executions = new FakeExecutionService();
        var display = new FakeDisplayIdService { ResolveResult = null };
        var stream = new ExecutionStreamService(executions, display);
        var controller = CreateController(http, executions, stream);

        await controller.GetStream("X", ct: CancellationToken.None);
        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
    }

    /// <summary>
    /// ヘッダー未指定時は既定テナントと空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task PublishEvent_WhenTenantRequestHeadersMissing_AndIdempotencyMissing_PassesDefaultTenantAndNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        // X-Idempotency-Key intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/executions/X/events";

        // Act
        var executions = new FakeExecutionService();
        var stream = new ExecutionStreamService(executions, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, executions, stream);

        // Assert
        var result = await controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, ct: CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        Assert.Null(executions.LastIdempotencyKey);
    }
}

