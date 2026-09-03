using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary><c>POST /v1/definitions/validate</c> の HTTP 契約。</summary>
public sealed class DefinitionsValidateApiIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private const string StatesYaml =
        """
        workflow:
          name: states-validate
        states:
          start:
            on:
              Completed:
                end: true
        """;

    private const string NodesYaml =
        """
        version: 1
        workflow:
          name: Minimal
        nodes:
          - name: start
            type: start
            next: endNode
          - name: endNode
            type: end
        """;

    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="factory">セキュリティ統合テスト用ファクトリ。</param>
    public DefinitionsValidateApiIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>合法な states YAML は 200 で、definitions 行は増えない。</summary>
    [Fact]
    public async Task PostValidate_StatesYaml_ReturnsOkWithoutPersisting()
    {
        // Arrange
        var before = await CountDefinitionsAsync();
        using var client = await CreateWriterClientAsync("validate-states@example.com");

        // Act
        using var response = await client.PostAsJsonAsync(
            new Uri("/v1/definitions/validate", UriKind.Relative),
            new { yaml = StatesYaml });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal("states-validate", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("compiledJson", out _));
        Assert.Equal(before, await CountDefinitionsAsync());
        Assert.Equal(0, await CountDefinitionVersionsAsync());
    }

    /// <summary>合法な nodes YAML は create と同じロード戦略で 200 になる。</summary>
    [Fact]
    public async Task PostValidate_NodesYaml_ReturnsOk()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("validate-nodes@example.com");

        // Act
        using var response = await client.PostAsJsonAsync(
            new Uri("/v1/definitions/validate", UriKind.Relative),
            new { yaml = NodesYaml });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal("Minimal", document.RootElement.GetProperty("name").GetString());
    }

    /// <summary>不正 YAML は 422 VALIDATION_ERROR になる。</summary>
    [Fact]
    public async Task PostValidate_InvalidYaml_ReturnsValidationError()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("validate-invalid@example.com");

        // Act
        using var response = await client.PostAsJsonAsync(
            new Uri("/v1/definitions/validate", UriKind.Relative),
            new { yaml = "::::not-yaml::::" });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "VALIDATION_ERROR",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, await CountDefinitionsAsync());
    }

    private async Task<HttpClient> CreateWriterClientAsync(string loginIdentifier)
    {
        var principalId = await _factory.SeedUserWithPermissionsAsync(
            loginIdentifier,
            "password",
            WellKnownPermissionKeys.DefinitionsWrite);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        return client;
    }

    private async Task<int> CountDefinitionsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Definitions.CountAsync();
    }

    private async Task<int> CountDefinitionVersionsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DefinitionVersions.CountAsync();
    }
}
