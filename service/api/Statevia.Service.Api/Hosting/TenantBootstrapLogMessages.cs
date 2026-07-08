using Microsoft.Extensions.Logging;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// <see cref="TenantBootstrapHostedService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class TenantBootstrapLogMessages
{
    /// <summary>開発管理者ブートストラップ設定が不足している。</summary>
    [LoggerMessage(
        EventId = 4030,
        Level = LogLevel.Warning,
        Message = "Dev admin bootstrap is enabled but TenantKey, Email, or Password is missing; skipping.")]
    public static partial void DevAdminBootstrapConfigMissing(this ILogger logger);

    /// <summary>開発環境で初回管理者ユーザーを作成した。</summary>
    [LoggerMessage(
        EventId = 4031,
        Level = LogLevel.Information,
        Message = "Development admin user created for tenant {TenantKey} (email: {Email}).")]
    public static partial void DevAdminUserCreated(this ILogger logger, string tenantKey, string email);
}
