using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// 認証シナリオテスト（要件2）。
/// </summary>
/// <remarks>
/// <para>login → JWT → /auth/me の HTTP フルスタック検証。</para>
/// <para>Docker 未検出時は自動スキップ。</para>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class AuthScenarioTests : ScenarioTestBase
{
    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    public AuthScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>
    /// seed 済みユーザーでログインし /auth/me が 200 を返すことを検証する。
    /// </summary>
    [SkippableFact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndMeSucceeds()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        var password = "Password1!";
        await _seeder.SeedUserAsync(username, password);

        // Act: login
        var loginResponse = await _http.LoginAsync("default", username, password);

        // Assert: token を取得できる
        Assert.NotEmpty(loginResponse.AccessToken);

        // Act: /auth/me
        using var client = _http.CreateAuthenticatedClient(loginResponse.AccessToken, "default");
        var meResponse = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var body = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal("default", body.GetProperty("tenantKey").GetString());
    }

    /// <summary>
    /// 誤パスワードでログインすると 401 を返すことを検証する。
    /// </summary>
    [SkippableFact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        await _seeder.SeedUserAsync(username, "CorrectPassword1!");

        using var client = Fixture.Factory!.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            tenantKey = "default",
            username,
            password = "WrongPassword!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
