using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// プロジェクト認可シナリオテスト（要件4）。
/// </summary>
/// <remarks>
/// <para>クロステナントの <c>project_access</c> 制御を HTTP で検証する。</para>
/// <list type="bullet">
///   <item>アクセス未付与の consumer は 404（存在秘匿）</item>
///   <item>Reader 付与 consumer は Start 403</item>
///   <item>Executor 付与 consumer は Start 201</item>
/// </list>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class ProjectAuthorizationScenarioTests : ScenarioTestBase
{
    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    public ProjectAuthorizationScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>
    /// オーナーが定義を作成し、アクセス未付与の別テナントユーザーが GET すると 404 を返すことを検証する。
    /// </summary>
    [SkippableFact]
    public async Task GetDefinition_WithoutProjectAccess_Returns404()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // オーナー（default テナント）が定義を作成する。
        var ownerUsername = $"owner-{suffix}";
        await _seeder.SeedUserAsync(ownerUsername, "Password1!");
        var ownerLogin = await _http.LoginAsync("default", ownerUsername, "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");

        var createResponse = await ownerClient.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-proj-{suffix}",
            yaml = WorkflowYamlFixtures.WaitWorkflow(suffix)
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = defJson.GetProperty("displayId").GetString()!;

        // consumer テナントを作成し、ユーザーを投入する（アクセスは付与しない）。
        var consumerTenantId = await _seeder.SeedTenantAsync(suffix);
        var consumerTenantKey = $"scenario-{suffix}";
        var consumerUsername = $"consumer-{suffix}";
        // アクセス未付与だが definitions.read パーミッションは持つユーザーを投入する。
        // project_access がないため 404（存在秘匿）になることを検証する。
        await _seeder.SeedUserInTenantAsync(
            consumerTenantId,
            consumerUsername,
            "Password1!",
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead]);
        var consumerLogin = await _http.LoginAsync(consumerTenantKey, consumerUsername, "Password1!");
        using var consumerClient = _http.CreateAuthenticatedClient(consumerLogin.AccessToken, consumerTenantKey);

        // Act
        var response = await consumerClient.GetAsync(
            new Uri($"/v1/definitions/{Uri.EscapeDataString(definitionId)}", UriKind.Relative));

        // Assert: 存在秘匿
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Reader 付与の consumer が Start すると 403 <c>PROJECT_ACCESS_DENIED</c> を返すことを検証する。
    /// </summary>
    [SkippableFact]
    public async Task StartExecution_WithReaderAccess_Returns403ProjectAccessDenied()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // オーナーが定義を作成する。
        var ownerUsername = $"owner-{suffix}";
        await _seeder.SeedUserAsync(ownerUsername, "Password1!");
        var ownerLogin = await _http.LoginAsync("default", ownerUsername, "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");

        var createResponse = await ownerClient.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-proj-{suffix}",
            yaml = WorkflowYamlFixtures.WaitWorkflow(suffix)
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = defJson.GetProperty("displayId").GetString()!;

        // consumer テナントを作成し、Reader アクセスを付与する。
        var consumerTenantId = await _seeder.SeedTenantAsync(suffix);
        var consumerTenantKey = $"scenario-{suffix}";
        await _seeder.SeedProjectWithAccessAsync(
            ScenarioTenantIds.DefaultTenantId,
            consumerTenantId,
            ProjectAccessRole.Reader);
        var consumerUsername = $"consumer-{suffix}";
        // Reader 付与。definitions.read + executions.write を持つが project role が Reader のため Start 不可。
        await _seeder.SeedUserInTenantAsync(
            consumerTenantId,
            consumerUsername,
            "Password1!",
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead, WellKnownPermissionKeys.ExecutionsWrite]);
        var consumerLogin = await _http.LoginAsync(consumerTenantKey, consumerUsername, "Password1!");
        using var consumerClient = _http.CreateAuthenticatedClient(consumerLogin.AccessToken, consumerTenantKey);

        // Act
        var response = await consumerClient.PostAsJsonAsync("/v1/executions", new { definitionId });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "PROJECT_ACCESS_DENIED");
    }

    /// <summary>
    /// Executor 付与の consumer が Start すると 201 Running を返すことを検証する。
    /// </summary>
    [SkippableFact]
    public async Task StartExecution_WithExecutorAccess_Returns201Running()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // オーナーが定義を作成する。
        var ownerUsername = $"owner-{suffix}";
        await _seeder.SeedUserAsync(ownerUsername, "Password1!");
        var ownerLogin = await _http.LoginAsync("default", ownerUsername, "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");

        var createResponse = await ownerClient.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-proj-{suffix}",
            yaml = WorkflowYamlFixtures.WaitWorkflow(suffix)
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var definitionId = defJson.GetProperty("displayId").GetString()!;

        // consumer テナントを作成し、Executor アクセスを付与する。
        var consumerTenantId = await _seeder.SeedTenantAsync(suffix);
        var consumerTenantKey = $"scenario-{suffix}";
        await _seeder.SeedProjectWithAccessAsync(
            ScenarioTenantIds.DefaultTenantId,
            consumerTenantId,
            ProjectAccessRole.Executor);
        var consumerUsername = $"consumer-{suffix}";
        // Executor 付与。definitions.read + executions.write を持ち、project role も Executor のため Start 可。
        await _seeder.SeedUserInTenantAsync(
            consumerTenantId,
            consumerUsername,
            "Password1!",
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead, WellKnownPermissionKeys.ExecutionsWrite]);
        var consumerLogin = await _http.LoginAsync(consumerTenantKey, consumerUsername, "Password1!");
        using var consumerClient = _http.CreateAuthenticatedClient(consumerLogin.AccessToken, consumerTenantKey);

        // Act
        var response = await consumerClient.PostAsJsonAsync("/v1/executions", new { definitionId });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var exec = await response.Content.ReadFromJsonAsync<JsonElement>();
        var status = exec.GetProperty("status").GetString();
        Assert.True(
            status is "Running" or "Queued",
            $"Expected Running or Queued, got: {status}");
    }
}
