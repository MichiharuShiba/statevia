using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Statevia.Core.Api.Tests.OpenApi;

/// <summary>
/// Swashbuckle が生成する OpenAPI ドキュメントのスモークテスト。
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<StateviaApiWebApplicationFactory>
{
    private readonly StateviaApiWebApplicationFactory _factory;

    /// <summary>
    /// <see cref="OpenApiDocumentTests"/> を生成する。
    /// </summary>
    /// <param name="factory">テスト用 Web アプリケーションファクトリ。</param>
    public OpenApiDocumentTests(StateviaApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Development 環境で swagger.json が取得でき、主要パスとスキーマが含まれる。
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ContainsRequiredPathsAndSchemas()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        AssertRequiredPath(paths, "/v1/health");
        AssertRequiredPath(paths, "/v1/definitions");
        AssertRequiredPath(paths, "/v1/executions");
        AssertRequiredPath(paths, "/v1/executions/{id}/stream");

        Assert.True(root.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.TryGetProperty("ErrorResponse", out _));
        Assert.True(schemas.TryGetProperty("ExecutionResponse", out _));
        Assert.True(ContainsPagedResultSchema(schemas));
    }

    /// <summary>
    /// 環境変数 <c>STATEVIA_EXPORT_OPENAPI</c> 指定時にリポジトリへ OpenAPI JSON を書き出す（手動 / スクリプト用）。
    /// </summary>
    [Fact]
    public async Task ExportOpenApiToRepository_WhenExportFlagSet()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STATEVIA_EXPORT_OPENAPI"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
        var json = await response.Content.ReadAsStringAsync();

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var outputPath = Path.Combine(repoRoot, "service", "api", "openapi", "core-api-v1.openapi.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(outputPath));
    }

    private static void AssertRequiredPath(JsonElement paths, string path)
    {
        Assert.True(
            paths.TryGetProperty(path, out _),
            string.Create(CultureInfo.InvariantCulture, $"OpenAPI paths に '{path}' がありません。"));
    }

    private static bool ContainsPagedResultSchema(JsonElement schemas)
    {
        foreach (var property in schemas.EnumerateObject())
        {
            if (property.Name.Contains("PagedResult", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
