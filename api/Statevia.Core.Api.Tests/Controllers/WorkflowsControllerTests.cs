using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Tests.Controllers;

public sealed class WorkflowsControllerTests
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

    private sealed class FakeWorkflowService : IWorkflowService
    {
        public Exception? ExceptionToThrow { get; set; }

        public string? LastTenantId { get; private set; }
        public string? LastIdempotencyKey { get; private set; }

        public WorkflowResponse StartResult { get; set; } = new WorkflowResponse();
        public List<WorkflowResponse> ListResult { get; set; } = new();
        public PagedResult<WorkflowResponse> ListPagedResult { get; set; } = new() { Items = new List<WorkflowResponse>(), TotalCount = 0, Offset = 0, Limit = 0, HasMore = false };
        public WorkflowResponse GetResult { get; set; } = new WorkflowResponse();
        public string GraphJsonResult { get; set; } = "{\"nodes\":[]}";
        public WorkflowViewDto ViewResult { get; set; } = new WorkflowViewDto();
        public ExecutionEventsResponseDto EventsResult { get; set; } = new ExecutionEventsResponseDto();

        public bool CancelCalled { get; private set; }
        public bool ResumeCalled { get; private set; }
        public bool PublishCalled { get; private set; }

        public string? CancelIdempotencyKey { get; private set; }
        public string? ResumeIdempotencyKey { get; private set; }
        public string? PublishIdempotencyKey { get; private set; }
        public string? ResumeResumeKey { get; private set; }

        public async Task<WorkflowResponse> StartAsync(string tenantId, StartWorkflowRequest request, string? idempotencyKey, string method, string path, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            LastIdempotencyKey = idempotencyKey;
            return StartResult;
        }

        public async Task<List<WorkflowResponse>> ListAsync(string tenantId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ListResult;
        }

        public async Task<PagedResult<WorkflowResponse>> ListPagedAsync(
            string tenantId,
            int offset,
            int limit,
            string? status,
            string? definitionId,
            string? nameContains,
            CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ListPagedResult;
        }

        public async Task<WorkflowResponse> GetWorkflowResponseAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return GetResult;
        }

        public async Task<string> GetGraphJsonAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return GraphJsonResult;
        }

        public async Task<WorkflowViewDto> GetWorkflowViewAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ViewResult;
        }

        public async Task<WorkflowViewDto> GetWorkflowViewAtSeqAsync(string tenantId, string idOrUuid, long atSeq, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ViewResult;
        }

        public async Task<ExecutionEventsResponseDto> ListEventsAsync(string tenantId, string idOrUuid, long afterSeq, int limit, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return EventsResult;
        }

        public async Task ResumeNodeAsync(string tenantId, string idOrUuid, string nodeId, string? resumeKey, string? idempotencyKey, string method, string path, CancellationToken ct)
        {
            ResumeCalled = true;
            ResumeResumeKey = resumeKey;
            ResumeIdempotencyKey = idempotencyKey;
            LastTenantId = tenantId;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task CancelAsync(string tenantId, string idOrUuid, string? idempotencyKey, string method, string path, CancellationToken ct)
        {
            CancelCalled = true;
            CancelIdempotencyKey = idempotencyKey;
            LastTenantId = tenantId;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task PublishEventAsync(string tenantId, string idOrUuid, string eventName, string? idempotencyKey, string method, string path, CancellationToken ct)
        {
            PublishCalled = true;
            PublishIdempotencyKey = idempotencyKey;
            LastTenantId = tenantId;
            LastIdempotencyKey = idempotencyKey;
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public Task UpdateProjectionFromEngineAsync(Guid workflowId, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static WorkflowsController CreateController(DefaultHttpContext http, FakeWorkflowService workflows, WorkflowStreamService stream)
    {
        return new WorkflowsController(workflows, stream)
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
        http.Request.Path = "/v1/workflows";
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        // Act
        var workflows = new FakeWorkflowService
        {
            StartResult = new WorkflowResponse { DisplayId = "WF-DISP-1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow, UpdatedAt = null, CancelRequested = false, RestartLost = false }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var request = new StartWorkflowRequest { DefinitionId = "def-1", Input = null };

        var result = await controller.Create(request, ct: CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(controller.Get), created.ActionName);
        Assert.True(created.RouteValues!.ContainsKey("id"));
        Assert.Equal("WF-DISP-1", created.RouteValues!["id"]);
        var value = Assert.IsType<WorkflowResponse>(created.Value);
        Assert.Equal("WF-DISP-1", value.DisplayId);
    }

    /// <summary>
    /// 一覧取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task List_LimitNull_ReturnsOkList()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var workflows = new FakeWorkflowService
        {
            ListResult = new List<WorkflowResponse>
            {
                new WorkflowResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow },
            }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.List(limit: null, offset: 0, status: null, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<List<WorkflowResponse>>(ok.Value);
        Assert.Single(value);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task List_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var workflows = new FakeWorkflowService
        {
            ListResult = new List<WorkflowResponse>
            {
                new WorkflowResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow },
            }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.List(limit: null, offset: 0, status: null, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsAssignableFrom<List<WorkflowResponse>>(ok.Value);
        Assert.Single(value);
        Assert.Equal("default", workflows.LastTenantId);
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
        var workflows = new FakeWorkflowService
        {
            ListPagedResult = new PagedResult<WorkflowResponse>
            {
                Items = new List<WorkflowResponse>
                {
                    new WorkflowResponse { DisplayId = "D2", ResourceId = Guid.NewGuid(), Status = "Completed", StartedAt = DateTime.UtcNow }
                },
                TotalCount = 1,
                Offset = 0,
                Limit = 1,
                HasMore = false
            }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.List(limit: 1, offset: 0, status: null, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<WorkflowResponse>>(ok.Value);
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

        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        await Assert.ThrowsAsync<ArgumentException>(() => controller.List(limit: 501, offset: 0, status: null, ct: CancellationToken.None));
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

        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.List(limit: 1, offset: -1, status: null, ct: CancellationToken.None));
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

        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => controller.List(limit: 0, offset: 0, status: null, ct: CancellationToken.None));
    }

    /// <summary>
    /// ワークフロー取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkWorkflowResponse()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var workflows = new FakeWorkflowService
        {
            GetResult = new WorkflowResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.Get("D1", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WorkflowResponse>(ok.Value);
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
        var workflows = new FakeWorkflowService { GraphJsonResult = "{\"nodes\":[]}" };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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
    public async Task GetState_ReturnsOkWorkflowView()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var workflows = new FakeWorkflowService
        {
            ViewResult = new WorkflowViewDto
            {
                DisplayId = "E1",
                ResourceId = Guid.NewGuid().ToString("D"),
                GraphId = "G1",
                Status = "ACTIVE",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = null,
                CancelRequested = false,
                RestartLost = false,
                Nodes = new List<WorkflowViewNodeDto>
                {
                    new WorkflowViewNodeDto
                    {
                        NodeId = "n1",
                        NodeType = "Task",
                        Status = "RUNNING",
                        Attempt = 1,
                        WorkerId = null,
                        WaitKey = null,
                        CanceledByExecution = false,
                        ConditionRouting = JsonDocument.Parse("""
                            {
                              "fact": "Completed",
                              "resolution": "matched_case",
                              "matchedCaseIndex": 0
                            }
                            """).RootElement.Clone()
                    }
                }
            }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.GetState("X", atSeq: 1, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WorkflowViewDto>(ok.Value);
        Assert.Single(model.Nodes);
        Assert.Equal("n1", model.Nodes[0].NodeId);
        Assert.True(model.Nodes[0].ConditionRouting.HasValue);
        Assert.Equal(
            "matched_case",
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
        var workflows = new FakeWorkflowService
        {
            EventsResult = new ExecutionEventsResponseDto
            {
                Events = new List<TimelineEventDto>
                {
                    new TimelineEventDto { Seq = 1, Type = "ExecutionStatusChanged", ExecutionId = "E1", To = "Running", At = DateTime.UtcNow.ToString("O") }
                },
                HasMore = false
            }
        };

        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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
        var workflows = new FakeWorkflowService();
        var display = new FakeDisplayIdService { ResolveResult = null };
        var stream = new WorkflowStreamService(workflows, display);
        var controller = CreateController(http, workflows, stream);

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
        http.Request.Path = "/v1/workflows/X/cancel";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.Cancel("X", tenantIdHeader: "t1", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.CancelCalled);
        Assert.Equal("idem", workflows.CancelIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/cancel";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.Cancel("X", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.CancelCalled);
        Assert.Null(workflows.CancelIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, tenantIdHeader: "t1", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Equal("Approve", workflows.ResumeResumeKey);
        Assert.Equal("idem", workflows.ResumeIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, tenantIdHeader: "t1", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Equal("Approve", workflows.ResumeResumeKey);
        Assert.Null(workflows.ResumeIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task ResumeNode_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Request.Headers["X-Idempotency-Key"] = "idem";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = "Approve" }, idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Equal("default", workflows.LastTenantId);
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
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode("X", "node-1", body: null, tenantIdHeader: "t1", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Null(workflows.ResumeResumeKey);
        Assert.Equal("idem", workflows.ResumeIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode("X", "node-1", new ResumeNodeRequest { ResumeKey = null }, tenantIdHeader: "t1", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Null(workflows.ResumeResumeKey);
        Assert.Equal("idem", workflows.ResumeIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/nodes/node-1/resume";

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no node") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.ResumeNode(
            "X",
            "node-1",
            new ResumeNodeRequest { ResumeKey = "Approve" },
            ct: CancellationToken.None,
            tenantIdHeader: "t1",
            idempotencyKey: "idem");

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.ResumeCalled);
        Assert.Equal("Approve", workflows.ResumeResumeKey);
        Assert.Equal("idem", workflows.ResumeIdempotencyKey);
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
        http.Request.Path = "/v1/workflows/X/events";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var result = await controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, tenantIdHeader: "t1", idempotencyKey: "idem", ct: CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(workflows.PublishCalled);
        Assert.Equal("idem", workflows.PublishIdempotencyKey);
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
        http.Request.Path = "/v1/workflows";
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        var workflows = new FakeWorkflowService
        {
            ExceptionToThrow = new NotFoundException("no def")
        };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var request = new StartWorkflowRequest { DefinitionId = "def-1", Input = null };

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
        http.Request.Path = "/v1/workflows/X/cancel";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

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

        var workflows = new FakeWorkflowService();
        var display = new ThrowingDisplayIdService(new NotFoundException("no wf"));
        var stream = new WorkflowStreamService(workflows, display);
        var controller = CreateController(http, workflows, stream);

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
        http.Request.Path = "/v1/workflows/X/events";

        var workflows = new FakeWorkflowService { ExceptionToThrow = new NotFoundException("no wf") };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        await Assert.ThrowsAsync<NotFoundException>(() => controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, ct: CancellationToken.None));
    }

    /// <summary>
    /// ヘッダー未指定時は既定テナントと空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task Create_WhenTenantHeaderMissing_AndIdempotencyMissing_PassesDefaultTenantAndNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/v1/workflows";
        // X-Tenant-Id intentionally missing
        // X-Idempotency-Key intentionally missing

        // Act
        var workflows = new FakeWorkflowService
        {
            StartResult = new WorkflowResponse { DisplayId = "WF-DISP-1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow }
        };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        var request = new StartWorkflowRequest { DefinitionId = "def-1", Input = null };

        // Assert
        var result = await controller.Create(request, ct: CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal("WF-DISP-1", ((WorkflowResponse)created.Value!).DisplayId);

        Assert.Equal("default", workflows.LastTenantId);
        Assert.Null(workflows.LastIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Cancel_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/workflows/X/cancel";
        http.Request.Headers["X-Idempotency-Key"] = "idem";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.Cancel("X", idempotencyKey: "idem", ct: CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        Assert.Equal("default", workflows.LastTenantId);
        Assert.Equal("idem", workflows.LastIdempotencyKey);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Get_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var workflows = new FakeWorkflowService { GetResult = new WorkflowResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Status = "Running", StartedAt = DateTime.UtcNow } };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.Get("X", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WorkflowResponse>(ok.Value);
        Assert.Equal("D1", model.DisplayId);

        Assert.Equal("default", workflows.LastTenantId);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetGraph_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var workflows = new FakeWorkflowService { GraphJsonResult = "{\"nodes\":[]}" };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.GetGraph("X", ct: CancellationToken.None);
        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Equal("{\"nodes\":[]}", content.Content);

        Assert.Equal("default", workflows.LastTenantId);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetState_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var workflows = new FakeWorkflowService
        {
            ViewResult = new WorkflowViewDto { DisplayId = "E1", ResourceId = Guid.NewGuid().ToString("D"), GraphId = "G1", Status = "ACTIVE", StartedAt = DateTime.UtcNow, UpdatedAt = null, CancelRequested = false, RestartLost = false, Nodes = new List<WorkflowViewNodeDto>() }
        };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.GetState("X", atSeq: 1, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<WorkflowViewDto>(ok.Value);

        Assert.Equal("default", workflows.LastTenantId);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task GetEvents_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing

        // Act
        var workflows = new FakeWorkflowService
        {
            EventsResult = new ExecutionEventsResponseDto { Events = new List<TimelineEventDto>(), HasMore = false }
        };
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.GetEvents("X", afterSeq: 0, limit: 10, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<ExecutionEventsResponseDto>(ok.Value);

        Assert.Equal("default", workflows.LastTenantId);
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 404 を設定する。
    /// </summary>
    [Fact]
    public async Task GetStream_WhenTenantHeaderMissing_Sets404()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Response.Body = new MemoryStream();

        // Act
        var workflows = new FakeWorkflowService();
        var display = new FakeDisplayIdService { ResolveResult = null };
        var stream = new WorkflowStreamService(workflows, display);
        var controller = CreateController(http, workflows, stream);

        await controller.GetStream("X", ct: CancellationToken.None);
        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
    }

    /// <summary>
    /// ヘッダー未指定時は既定テナントと空値の冪等キーを渡す。
    /// </summary>
    [Fact]
    public async Task PublishEvent_WhenTenantHeaderMissing_AndIdempotencyMissing_PassesDefaultTenantAndNullIdempotencyKey()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        // X-Idempotency-Key intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/workflows/X/events";

        // Act
        var workflows = new FakeWorkflowService();
        var stream = new WorkflowStreamService(workflows, new FakeDisplayIdService { ResolveResult = null });
        var controller = CreateController(http, workflows, stream);

        // Assert
        var result = await controller.PublishEvent("X", new PublishEventRequest { Name = "Approve" }, ct: CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        Assert.Equal("default", workflows.LastTenantId);
        Assert.Null(workflows.LastIdempotencyKey);
    }
}

