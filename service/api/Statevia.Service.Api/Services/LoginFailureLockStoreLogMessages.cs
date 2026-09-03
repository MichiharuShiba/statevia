namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="LoginFailureLockStore"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class LoginFailureLockStoreLogMessages
{
    /// <summary>失敗回数の書き込みに失敗した。呼び出し側は 401 を維持する。</summary>
    [LoggerMessage(
        EventId = 4050,
        Level = LogLevel.Error,
        Message = "Failed to record login failure. TenantKey={tenantKey} Username={username}")]
    public static partial void RecordFailureFailed(
        this ILogger logger,
        Exception ex,
        string tenantKey,
        string username);

    /// <summary>成功後のロック解除に失敗した。トークン発行は維持する。</summary>
    [LoggerMessage(
        EventId = 4051,
        Level = LogLevel.Error,
        Message = "Failed to reset login failure lock. TenantKey={tenantKey} Username={username}")]
    public static partial void ResetFailed(
        this ILogger logger,
        Exception ex,
        string tenantKey,
        string username);
}
