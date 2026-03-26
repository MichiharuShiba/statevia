using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Tests.Controllers;

public sealed class DefinitionsControllerTests
{
    private sealed class FakeDefinitionService : IDefinitionService
    {
        public Exception? ExceptionToThrow { get; set; }
        public string? LastTenantId { get; private set; }

        public DefinitionResponse CreateResult { get; set; } = new DefinitionResponse();
        public List<DefinitionResponse> ListResult { get; set; } = new();
        public PagedResult<DefinitionResponse> ListPagedResult { get; set; } = new() { Items = new List<DefinitionResponse>(), TotalCount = 0, Offset = 0, Limit = 0, HasMore = false };
        public DefinitionResponse GetResult { get; set; } = new DefinitionResponse();

        public async Task<DefinitionResponse> CreateAsync(string tenantId, CreateDefinitionRequest request, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return CreateResult;
        }

        public async Task<List<DefinitionResponse>> ListAsync(string tenantId, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ListResult;
        }

        public async Task<PagedResult<DefinitionResponse>> ListPagedAsync(string tenantId, int offset, int limit, string? nameContains, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return ListPagedResult;
        }

        public async Task<DefinitionResponse> GetAsync(string tenantId, string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            LastTenantId = tenantId;
            return GetResult;
        }
    }

    /// <summary>
    /// 作成結果として作成済み応答を返す。
    /// </summary>
    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/definitions";

        // Act
        var fake = new FakeDefinitionService
        {
            CreateResult = new DefinitionResponse { DisplayId = "DEF-1", ResourceId = Guid.NewGuid(), Name = "n", CreatedAt = DateTime.UtcNow }
        };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var req = new CreateDefinitionRequest { Name = "n", Yaml = "workflow: {}" };
        var res = await controller.Create(req, CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        Assert.Equal(nameof(controller.Get), created.ActionName);
        Assert.Equal("DEF-1", created.RouteValues!["id"]);
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
        var fake = new FakeDefinitionService
        {
            ListResult = new List<DefinitionResponse>
            {
                new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow }
            }
        };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        // Assert
        var res = await controller.List(limit: null, offset: 0, name: null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res);
        var list = Assert.IsType<List<DefinitionResponse>>(ok.Value);
        Assert.Single(list);
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
        var fake = new FakeDefinitionService
        {
            ListResult = new List<DefinitionResponse>
            {
                new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow }
            }
        };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        // Assert
        var res = await controller.List(limit: null, offset: 0, name: null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res);
        var list = Assert.IsType<List<DefinitionResponse>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("default", fake.LastTenantId);
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
        var fake = new FakeDefinitionService
        {
            ListPagedResult = new PagedResult<DefinitionResponse>
            {
                Items = new List<DefinitionResponse> { new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow } },
                TotalCount = 1,
                Offset = 0,
                Limit = 1,
                HasMore = false
            }
        };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        // Assert
        var res = await controller.List(limit: 1, offset: 0, name: null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res);
        var paged = Assert.IsType<PagedResult<DefinitionResponse>>(ok.Value);
        Assert.False(paged.HasMore);
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

        var fake = new FakeDefinitionService();
        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => controller.List(limit: 501, offset: 0, name: null, CancellationToken.None));
    }

    /// <summary>
    /// 単体取得で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOk()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var expected = new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow };

        var fake = new FakeDefinitionService { GetResult = expected };
        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        // Assert
        var res = await controller.Get("id", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<DefinitionResponse>(ok.Value);
        Assert.Equal(expected.DisplayId, body.DisplayId);
    }

    /// <summary>
    /// サービスが未検出例外を投げたとき同じ例外を返す。
    /// </summary>
    [Fact]
    public async Task Create_WhenServiceThrowsNotFoundException_ThrowsNotFoundException()
    {
        // Act & Assert
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        http.Request.Method = "POST";
        http.Request.Path = "/v1/definitions";

        var fake = new FakeDefinitionService { ExceptionToThrow = new NotFoundException("no def") };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var req = new CreateDefinitionRequest { Name = "n", Yaml = "workflow: {}" };

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Create(req, CancellationToken.None));
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

        var fake = new FakeDefinitionService { ExceptionToThrow = new NotFoundException("no def") };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Get("id", CancellationToken.None));
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Create_WhenTenantHeaderMissing_PassesDefaultTenant()
    {
        // Arrange
        var http = new DefaultHttpContext();
        // X-Tenant-Id intentionally missing
        http.Request.Method = "POST";
        http.Request.Path = "/v1/definitions";

        // Act
        var fake = new FakeDefinitionService
        {
            CreateResult = new DefinitionResponse { DisplayId = "DEF-1", ResourceId = Guid.NewGuid(), Name = "n", CreatedAt = DateTime.UtcNow }
        };

        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var req = new CreateDefinitionRequest { Name = "n", Yaml = "workflow: {}" };
        var res = await controller.Create(req, CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        Assert.Equal("DEF-1", created.RouteValues!["id"]);
        Assert.Equal("default", fake.LastTenantId);
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
        var fake = new FakeDefinitionService
        {
            GetResult = new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow }
        };

        // Act
        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        // Assert
        var res = await controller.Get("id", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<DefinitionResponse>(ok.Value);
        Assert.Equal("D1", body.DisplayId);
        Assert.Equal("default", fake.LastTenantId);
    }
}

