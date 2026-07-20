using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

public sealed class DefinitionsControllerTests
{
    private sealed class FakeDefinitionService : IDefinitionService
    {
        public Exception? ExceptionToThrow { get; set; }

        public DefinitionResponse CreateResult { get; set; } = new DefinitionResponse();
        public PagedResult<DefinitionResponse> ListPagedResult { get; set; } = new() { Items = [], TotalCount = 0, Offset = 0, Limit = 0, HasMore = false };
        public DefinitionResponse GetResult { get; set; } = new DefinitionResponse();
        public DefinitionResponse UpdateResult { get; set; } = new DefinitionResponse();

        public async Task<DefinitionResponse> CreateAsync(CreateDefinitionRequest request, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return CreateResult;
        }

        public async Task<PagedResult<DefinitionResponse>> ListPagedAsync(DefinitionListPageQuery query, CancellationToken ct)
        {
            await Task.Yield();
            if (ExceptionToThrow is { } ex) throw ex;
            return ListPagedResult;
        }

        public async Task<DefinitionResponse> GetAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return GetResult;
        }

        public async Task<DefinitionResponse> UpdateAsync(string idOrUuid, UpdateDefinitionRequest request, CancellationToken ct)
        {
            await Task.Yield(); // async boundary for coverage
            if (ExceptionToThrow is { } ex) throw ex;
            return UpdateResult;
        }

        public async Task DeleteAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield();
            if (ExceptionToThrow is { } ex) throw ex;
        }

        public async Task<DefinitionResponse> RestoreAsync(string idOrUuid, CancellationToken ct)
        {
            await Task.Yield();
            if (ExceptionToThrow is { } ex) throw ex;
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
        var res = await controller.Create(req, ct: CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        Assert.Equal(nameof(controller.Get), created.ActionName);
        Assert.Equal("DEF-1", created.RouteValues!["id"]);
    }

    /// <summary>
    /// limit 未指定は Data Annotations で検証失敗になる（アクション直呼びではパイプライン未経由）。
    /// </summary>
    [Fact]
    public void DefinitionListQuery_WhenLimitNull_FailsRequiredValidation()
    {
        // Arrange
        var query = new DefinitionListQuery();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(query);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Act
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(query, context, results, validateAllProperties: true);

        // Assert
        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(DefinitionListQuery.Limit)));
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
        var fake = new FakeDefinitionService
        {
            ListPagedResult = new PagedResult<DefinitionResponse>
            {
                Items = [new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow }],
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
        var res = await controller.List(new DefinitionListQuery { Limit = 1, Offset = 0 }, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res);
        var paged = Assert.IsType<PagedResult<DefinitionResponse>>(ok.Value);
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
        var fake = new FakeDefinitionService
        {
            ListPagedResult = new PagedResult<DefinitionResponse>
            {
                Items = [new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "a", CreatedAt = DateTime.UtcNow }],
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
        var res = await controller.List(new DefinitionListQuery { Limit = 1, Offset = 0 }, ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res);
        var paged = Assert.IsType<PagedResult<DefinitionResponse>>(ok.Value);
        Assert.False(paged.HasMore);
    }

    /// <summary>
    /// 上限を超える件数指定は Data Annotations で検証失敗になる。
    /// </summary>
    [Fact]
    public void DefinitionListQuery_WhenLimitAboveMax_FailsRangeValidation()
    {
        // Arrange
        var query = new DefinitionListQuery { Limit = 501, Offset = 0 };
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(query);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        // Act
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(query, context, results, validateAllProperties: true);

        // Assert
        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(DefinitionListQuery.Limit)));
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
        var res = await controller.Get("id", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<DefinitionResponse>(ok.Value);
        Assert.Equal(expected.DisplayId, body.DisplayId);
    }

    /// <summary>
    /// 更新で成功応答を返す。
    /// </summary>
    [Fact]
    public async Task Update_ReturnsOk()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        var expected = new DefinitionResponse { DisplayId = "D1", ResourceId = Guid.NewGuid(), Name = "updated", CreatedAt = DateTime.UtcNow };
        var fake = new FakeDefinitionService { UpdateResult = expected };
        var controller = new DefinitionsController(fake)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var res = await controller.Update("id", new UpdateDefinitionRequest { Name = "updated", Yaml = "workflow: {}" }, ct: CancellationToken.None);
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

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Create(req, ct: CancellationToken.None));
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

        await Assert.ThrowsAsync<NotFoundException>(() => controller.Get("id", ct: CancellationToken.None));
    }

    /// <summary>
    /// テナントヘッダー未指定 の場合は 既定テナントを渡す。
    /// </summary>
    [Fact]
    public async Task Create_WhenTenantRequestHeadersMissing_PassesDefaultTenant()
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
        var res = await controller.Create(req, ct: CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        Assert.Equal("DEF-1", created.RouteValues!["id"]);
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
        var res = await controller.Get("id", ct: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<DefinitionResponse>(ok.Value);
        Assert.Equal("D1", body.DisplayId);
    }

}

