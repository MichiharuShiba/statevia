using System.Net;
using System.Net.Http.Headers;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>Runtime API の認証必須化（ミドルウェア統合）。</summary>
public sealed class RuntimeApiAuthenticationIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public RuntimeApiAuthenticationIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary><c>X-Tenant-Id</c> のみでは 401。</summary>
    [Fact]
    public async Task GetDefinitions_HeaderOnly_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>JWT 付きでは 401 にならない。</summary>
    [Fact]
    public async Task GetDefinitions_WithJwt_ReturnsSuccessStatus()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("runtime@example.com", "password");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>API キー付きでは 401 にならない。</summary>
    [Fact]
    public async Task GetDefinitions_WithApiKey_ReturnsSuccessStatus()
    {
        // Arrange
        var plainKey = await _factory.SeedApiKeyPlainTextAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", plainKey);

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>health は認証なしで到達できる。</summary>
    [Fact]
    public async Task GetHealth_WithoutAuth_ReturnsOk()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("/v1/health", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
