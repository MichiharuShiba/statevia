using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Tests.Controllers;

public sealed class GraphsControllerTests
{
    /// <summary>
    /// 識別子が空白のみのとき検証失敗応答を返す。
    /// </summary>
    [Fact]
    public async Task GetByGraphId_WhenGraphIdIsWhitespace_ReturnsValidationError422()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";
        var fakeSvc = new FakeGraphDefinitionService();

        // Act
        var controller = new GraphsController(fakeSvc)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var result = await controller.GetByGraphId("   ", CancellationToken.None);

        // Assert
        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, obj.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(obj.Value);
        Assert.Equal("VALIDATION_ERROR", payload.Error.Code);
    }

    /// <summary>
    /// 取得成功時に成功応答でグラフを返す。
    /// </summary>
    [Fact]
    public async Task GetByGraphId_WhenSuccess_ReturnsOkGraph()
    {
        // Arrange
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Tenant-Id"] = "t1";

        // Act
        var expected = new GraphDefinitionResponse
        {
            GraphId = "g1",
            Nodes = new GraphNodeDefinition[0],
            Edges = new GraphEdgeDefinition[0],
            Ui = null
        };

        var fakeSvc = new FakeGraphDefinitionService
        {
            ResolveResult = expected
        };

        var controller = new GraphsController(fakeSvc)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http }
        };

        var result = await controller.GetByGraphId("graph-id", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(expected.GraphId, ((GraphDefinitionResponse)ok.Value!).GraphId);
    }

    private sealed class FakeGraphDefinitionService : IGraphDefinitionService
    {
        public GraphDefinitionResponse? ResolveResult { get; set; }
        public async Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, string tenantId, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return ResolveResult ?? new GraphDefinitionResponse();
        }
    }
}

