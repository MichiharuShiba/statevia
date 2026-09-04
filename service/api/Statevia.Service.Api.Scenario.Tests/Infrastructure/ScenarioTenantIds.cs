namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテストで使用するテナント UUID の定数。
/// </summary>
/// <remarks>
/// マイグレーションの seed データ（<c>TenantBootstrap</c>）と一致させる。
/// </remarks>
internal static class ScenarioTenantIds
{
    /// <summary><c>tenant_key = default</c> の内部 UUID（マイグレーション seed と一致）。</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-4000-8000-000000000001");
}
