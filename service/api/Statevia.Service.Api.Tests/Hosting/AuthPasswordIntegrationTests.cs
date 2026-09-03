using Statevia.Service.Api.Contracts.Auth;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>認証 API のパスワード更新統合テスト。</summary>
public sealed class AuthPasswordIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AuthPasswordIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>現行パスワード一致で本人更新は 204。</summary>
    [Fact]
    public async Task ChangeOwnPassword_MatchingCurrent_ReturnsNoContent()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("self-password@example.com", "password123");
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PutAsJsonAsync(
            new Uri("/v1/auth/me/password", UriKind.Relative),
            new ChangeOwnPasswordRequest
            {
                CurrentPassword = "password123",
                NewPassword = "nextpass01"
            });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>現行パスワード不一致は 401。</summary>
    [Fact]
    public async Task ChangeOwnPassword_WrongCurrent_ReturnsUnauthorized()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("self-wrong@example.com", "password123");
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PutAsJsonAsync(
            new Uri("/v1/auth/me/password", UriKind.Relative),
            new ChangeOwnPasswordRequest
            {
                CurrentPassword = "wrong",
                NewPassword = "nextpass01"
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>API キーでは本人更新できない。</summary>
    [Fact]
    public async Task ChangeOwnPassword_ApiKey_ReturnsUnauthorized()
    {
        // Arrange
        var plainKey = await _factory.SeedApiKeyPlainTextAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", plainKey);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");

        // Act
        var response = await client.PutAsJsonAsync(
            new Uri("/v1/auth/me/password", UriKind.Relative),
            new ChangeOwnPasswordRequest
            {
                CurrentPassword = "password123",
                NewPassword = "nextpass01"
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>新パスワードが短いと 422。</summary>
    [Fact]
    public async Task ChangeOwnPassword_ShortNewPassword_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("self-short@example.com", "password123");
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PutAsJsonAsync(
            new Uri("/v1/auth/me/password", UriKind.Relative),
            new ChangeOwnPasswordRequest
            {
                CurrentPassword = "password123",
                NewPassword = "short"
            });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        return client;
    }
}
