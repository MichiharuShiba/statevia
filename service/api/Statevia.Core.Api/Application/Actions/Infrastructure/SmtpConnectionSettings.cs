namespace Statevia.Core.Api.Application.Actions.Infrastructure;

/// <summary>SMTP 送信に使う接続設定（workflow input とは分離）。</summary>
/// <param name="Host">SMTP ホスト。</param>
/// <param name="Port">SMTP ポート。</param>
/// <param name="User">認証ユーザー（任意）。</param>
/// <param name="Password">認証パスワード（任意）。</param>
/// <param name="DefaultFrom">notify input で from 省略時の既定差出人（任意）。</param>
internal sealed record SmtpConnectionSettings(
    string Host,
    int Port,
    string? User,
    string? Password,
    string? DefaultFrom);

/// <summary>SMTP 接続設定の解決キー（テナント / プロファイルは将来拡張）。</summary>
/// <param name="TenantId">テナント UUID 文字列（Database ソース向け）。</param>
/// <param name="Profile">非機微プロファイル名（例: <c>default</c>, <c>billing</c>）。</param>
internal sealed record SmtpConnectionSettingsRequest(string? TenantId = null, string? Profile = null);
