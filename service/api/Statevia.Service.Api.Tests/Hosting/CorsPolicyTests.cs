using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Statevia.Service.Api.Tests.OpenApi;
using System.Net;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>既定 CORS ポリシー（許可オリジン allow-list）の検証。</summary>
public sealed class CorsPolicyTests
{
    /// <summary>許可オリジンからの GET では ACAO が返る。</summary>
    [Fact]
    public async Task Get_WhenOriginIsAllowed_ReturnsAccessControlAllowOrigin()
    {
        // Arrange
        await using var factory = CreateFactoryWithOrigins("http://localhost:3000");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal("http://localhost:3000", Assert.Single(values));
    }

    /// <summary>未許可オリジンからの GET では ACAO を付けない。</summary>
    [Fact]
    public async Task Get_WhenOriginIsNotAllowed_OmitsAccessControlAllowOrigin()
    {
        // Arrange
        await using var factory = CreateFactoryWithOrigins("http://localhost:3000");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static WebApplicationFactory<Program> CreateFactoryWithOrigins(params string[] origins)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < origins.Length; i++)
        {
            values[$"Statevia:Cors:AllowedOrigins:{i}"] = origins[i];
        }

        return new StateviaApiWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));
        });
    }
}
