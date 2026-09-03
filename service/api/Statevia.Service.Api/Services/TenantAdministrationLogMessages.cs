namespace Statevia.Service.Api.Services;

/// <summary>
/// <see cref="TenantAdministrationService"/> 用のログ（<see cref="LoggerMessageAttribute"/>）。
/// </summary>
internal static partial class TenantAdministrationLogMessages
{
    /// <summary>テナント管理者が対象ユーザーのパスワードを上書きした。</summary>
    [LoggerMessage(
        EventId = 4040,
        Level = LogLevel.Information,
        Message = "Admin updated user password. ActorPrincipalId={actorPrincipalId} TargetUserId={targetUserId}")]
    public static partial void UserPasswordUpdatedByAdmin(
        this ILogger logger,
        Guid actorPrincipalId,
        Guid targetUserId);
}
