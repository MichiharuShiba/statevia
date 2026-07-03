using System.Net;
using System.Net.Http.Headers;

using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>Runtime API の global permission 認可（統合）。</summary>
public sealed class RuntimeApiAuthorizationIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public RuntimeApiAuthorizationIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>permission 未付与 JWT は definitions 一覧で 403。</summary>
    [Fact]
    public async Task GetDefinitions_JwtWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("no-perm@example.com", "password", isTenantAdmin: false);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>definitions.read 付与 JWT は definitions 一覧を取得できる。</summary>
    [Fact]
    public async Task GetDefinitions_JwtWithDefinitionsRead_ReturnsOk()
    {
        // Arrange
        var principalId = await _factory.SeedUserWithPermissionsAsync(
            "def-reader@example.com",
            "password",
            WellKnownPermissionKeys.DefinitionsRead);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>executions.read のみの API キーは definitions 一覧で 403。</summary>
    [Fact]
    public async Task GetDefinitions_ApiKeyWithExecutionsReadOnly_ReturnsForbidden()
    {
        // Arrange
        var plainKey = await _factory.SeedApiKeyPlainTextAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", plainKey);

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=10", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>executions.read の API キーは executions 一覧を取得できる。</summary>
    [Fact]
    public async Task GetExecutions_ApiKeyWithExecutionsRead_ReturnsOk()
    {
        // Arrange
        var plainKey = await _factory.SeedApiKeyPlainTextAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", plainKey);

        // Act
        var response = await client.GetAsync(new Uri("/v1/executions?limit=10", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        return client;
    }
}
