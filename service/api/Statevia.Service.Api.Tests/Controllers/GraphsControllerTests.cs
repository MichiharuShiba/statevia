using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

public sealed class GraphsControllerTests
{
    /// <summary>
    /// 識別子が空白のみのとき検証失敗応答を返す。
    /// </summary>
    [Fact]
    public async Task GetByGraphId_WhenGraphIdIsWhitespace_ReturnsValidationError422()
    {
        // Arrange
        var fakeSvc = new FakeGraphDefinitionService();

        // Act
        var controller = new GraphsController(fakeSvc);
        var result = await controller.GetByGraphId("   ", ct: CancellationToken.None);

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
        // Act
        var expected = new GraphDefinitionResponse
        {
            GraphId = "g1",
            Nodes = Array.Empty<GraphNodeDefinition>(),
            Edges = Array.Empty<GraphEdgeDefinition>()
        };

        var fakeSvc = new FakeGraphDefinitionService
        {
            ResolveResult = expected
        };

        var controller = new GraphsController(fakeSvc);
        var result = await controller.GetByGraphId("graph-id", ct: CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(expected.GraphId, ((GraphDefinitionResponse)ok.Value!).GraphId);
    }

    private sealed class FakeGraphDefinitionService : IGraphDefinitionService
    {
        public GraphDefinitionResponse? ResolveResult { get; set; }
        public async Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, CancellationToken ct = default)
        {
            await Task.Yield(); // async boundary for coverage
            return ResolveResult ?? new GraphDefinitionResponse();
        }
    }
}

