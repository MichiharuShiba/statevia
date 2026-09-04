using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// 定義・ワークフローシナリオテスト（要件3）。
/// </summary>
/// <remarks>
/// <para>JWT 認証下で定義作成 → WF 開始 → Cancel → Cancelled ポーリングを検証する。</para>
/// <para>同一 <c>X-Idempotency-Key</c> で 2 回 Start したとき同一 display ID が返ることも検証する。</para>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class DefinitionWorkflowScenarioTests : ScenarioTestBase
{
    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    public DefinitionWorkflowScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>
    /// 定義を作成し、WF を Start → Cancel → Cancelled になることを検証する。
    /// </summary>
    [SkippableFact]
    public async Task StartAndCancel_WaitWorkflow_ReachesCancelledStatus()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        await _seeder.SeedUserAsync(username, "Password1!");
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // Act: 定義作成
        var yaml = WorkflowYamlFixtures.WaitWorkflow(suffix);
        var createResponse = await client.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-wait-{suffix}",
            yaml
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = defJson.GetProperty("displayId").GetString()!;

        // Act: WF 開始
        var startResponse = await client.PostAsJsonAsync("/v1/executions", new
        {
            definitionId
        });
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var execJson = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var executionId = execJson.GetProperty("displayId").GetString()!;
        Output.WriteLine($"Execution started: {executionId}");

        // Act: Cancel
        var cancelResponse = await client.PostAsJsonAsync($"/v1/executions/{executionId}/cancel", new { });
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        // Assert: Cancelled にポーリング
        var finalDoc = await ScenarioHttpClient.PollExecutionStatusAsync(client, executionId, "Cancelled");
        Assert.Equal("Cancelled", finalDoc.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// 同一 <c>X-Idempotency-Key</c> で 2 回 Start すると同一 display ID が返ることを検証する。
    /// </summary>
    [SkippableFact]
    public async Task Start_WithSameIdempotencyKey_ReturnsSameExecutionDisplayId()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        await _seeder.SeedUserAsync(username, "Password1!");
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // 定義作成
        var yaml = WorkflowYamlFixtures.WaitWorkflow(suffix);
        var createResponse = await client.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-wait-{suffix}",
            yaml
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = defJson.GetProperty("displayId").GetString()!;

        var idempotencyKey = Guid.NewGuid().ToString("N");

        // Act: 1 回目 Start
        using (var req1 = new HttpRequestMessage(HttpMethod.Post, "/v1/executions")
        {
            Content = JsonContent.Create(new { definitionId })
        })
        {
            req1.Headers.Add("X-Idempotency-Key", idempotencyKey);
            var res1 = await client.SendAsync(req1);
            Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
            var exec1 = await res1.Content.ReadFromJsonAsync<JsonElement>();
            var displayId1 = exec1.GetProperty("displayId").GetString()!;

            // Act: 2 回目 Start（同一 Key）
            using var req2 = new HttpRequestMessage(HttpMethod.Post, "/v1/executions")
            {
                Content = JsonContent.Create(new { definitionId })
            };
            req2.Headers.Add("X-Idempotency-Key", idempotencyKey);
            var res2 = await client.SendAsync(req2);
            Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
            var exec2 = await res2.Content.ReadFromJsonAsync<JsonElement>();
            var displayId2 = exec2.GetProperty("displayId").GetString()!;

            // Assert: 同一 display ID
            Assert.Equal(displayId1, displayId2);
        }
    }
}
