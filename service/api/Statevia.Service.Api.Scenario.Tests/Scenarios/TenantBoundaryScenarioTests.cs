using System.Net;
using System.Net.Http.Json;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// テナント境界シナリオテスト（要件2-3）。
/// </summary>
/// <remarks>
/// JWT の <c>tenant_key</c> と <c>X-Tenant-Id</c> ヘッダが不一致の場合に
/// 403 <c>TENANT_HEADER_MISMATCH</c> が返ることを検証する。
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class TenantBoundaryScenarioTests : ScenarioTestBase
{
    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    public TenantBoundaryScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>
    /// JWT が <c>default</c> テナント向けのとき、<c>X-Tenant-Id: other-tenant</c> を渡すと
    /// 403 <c>TENANT_HEADER_MISMATCH</c> が返ることを検証する。
    /// </summary>
    [SkippableFact]
    public async Task AuthenticatedRequest_WithMismatchedTenantHeader_Returns403TenantHeaderMismatch()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        await _seeder.SeedUserAsync(username, "Password1!");
        var login = await _http.LoginAsync("default", username, "Password1!");

        // JWT は default テナント向けだが、ヘッダは mismatched-tenant を指定する。
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "mismatched-tenant");

        // Act
        var response = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "TENANT_HEADER_MISMATCH");
    }
}
