using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Tests.Controllers;

/// <summary><see cref="HealthController"/> の死活 API。</summary>
public sealed class HealthControllerTests
{
    /// <summary>GET /v1/health は status=ok を返す。</summary>
    [Fact]
    public void Get_ReturnsOkWithStatusOk()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Get();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ok", payload.Status);
    }
}
