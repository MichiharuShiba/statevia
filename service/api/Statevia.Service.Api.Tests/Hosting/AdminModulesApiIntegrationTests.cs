using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>Module 管理 API の統合テスト。</summary>
public sealed class AdminModulesApiIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AdminModulesApiIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>未認証は module 一覧で 401。</summary>
    [Fact]
    public async Task ListModules_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");

        // Act
        var response = await client.GetAsync(new Uri("/v1/admin/modules", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>非管理者は module 一覧で 403。</summary>
    [Fact]
    public async Task ListModules_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("member-modules@example.com", "password", isTenantAdmin: false);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/admin/modules", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>管理者は module 一覧を取得できる。</summary>
    [Fact]
    public async Task ListModules_TenantAdmin_ReturnsOk()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("admin-modules@example.com", "password", isTenantAdmin: true);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/admin/modules", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await response.Content.ReadFromJsonAsync<List<AdminModuleListItemDto>>();
        Assert.NotNull(modules);
    }

    /// <summary>管理者は reload を実行できる。</summary>
    [Fact]
    public async Task ReloadModules_TenantAdmin_ReturnsNoContent()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("admin-reload@example.com", "password", isTenantAdmin: true);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PostAsync(new Uri("/internal/modules/reload", UriKind.Relative), content: null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>未認証は reload で 401。</summary>
    [Fact]
    public async Task ReloadModules_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");

        // Act
        var response = await client.PostAsync(new Uri("/internal/modules/reload", UriKind.Relative), content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>非管理者は reload で 403。</summary>
    [Fact]
    public async Task ReloadModules_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("member-reload@example.com", "password", isTenantAdmin: false);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PostAsync(new Uri("/internal/modules/reload", UriKind.Relative), content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        return client;
    }
}
