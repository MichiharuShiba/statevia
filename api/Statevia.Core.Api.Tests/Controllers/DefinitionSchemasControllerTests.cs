using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Tests.Controllers;

public sealed class DefinitionSchemasControllerTests
{
    private sealed class FakeDefinitionSchemaService : IDefinitionSchemaService
    {
        public object GetNodesSchemaDocument() => new { type = "object" };
        public string GetNodesSchemaVersion() => "1.0.0";
        public int GetNodesVersion() => 1;
    }

    /// <summary>
    /// nodes スキーマ取得 API が schemaVersion と nodesVersion を返す。
    /// </summary>
    [Fact]
    public void GetNodesSchema_ReturnsSchemaPayload()
    {
        // Arrange
        var controller = new DefinitionSchemasController(new FakeDefinitionSchemaService());

        // Act
        var res = controller.GetNodesSchema();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<DefinitionNodesSchemaResponse>(ok.Value);
        Assert.Equal("1.0.0", body.SchemaVersion);
        Assert.Equal(1, body.NodesVersion);
        Assert.NotNull(body.Schema);
    }
}
