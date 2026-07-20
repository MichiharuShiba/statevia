using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Contracts.Validation;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="GraphsController"/> のユニットテスト。</summary>
public sealed class GraphsControllerTests
{
    /// <summary>
    /// graphId の空白のみは NotWhitespace で拒否する（アクション直呼びではパイプライン未経由）。
    /// </summary>
    [Fact]
    public void GraphId_NotWhitespaceAttribute_RejectsWhitespace()
    {
        // Arrange
        var attribute = new NotWhitespaceAttribute { ErrorMessage = "graphId is required" };

        // Act
        var valid = attribute.IsValid("   ");

        // Assert
        Assert.False(valid);
    }

    /// <summary>
    /// 取得成功時に成功応答でグラフを返す。
    /// </summary>
    [Fact]
    public async Task GetByGraphId_WhenSuccess_ReturnsOkGraph()
    {
        // Arrange
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

        // Act
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
            await Task.Yield();
            return ResolveResult ?? new GraphDefinitionResponse();
        }
    }
}
