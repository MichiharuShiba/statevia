namespace Statevia.Service.Api.Application.Actions.Infrastructure;

/// <summary>
/// notify builtin の SMTP 接続設定を解決する。
/// 実装は Environment / Database / KMS 等に差し替え可能。
/// </summary>
internal interface ISmtpConnectionSettingsProvider
{
    /// <summary>送信に使う SMTP 接続設定を取得する。</summary>
    /// <param name="request">テナント・プロファイル等の解決キー。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>接続設定。</returns>
    Task<SmtpConnectionSettings> GetAsync(SmtpConnectionSettingsRequest request, CancellationToken cancellationToken);
}
