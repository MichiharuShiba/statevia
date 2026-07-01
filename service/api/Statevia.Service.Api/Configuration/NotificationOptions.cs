namespace Statevia.Service.Api.Configuration;

/// <summary>notify builtin の SMTP 接続設定の読み取り元。</summary>
internal enum NotificationSmtpSettingsSource
{
    /// <summary>環境変数 / <see cref="NotificationOptions.Smtp"/> 設定。</summary>
    Environment = 0,

    /// <summary>テナント DB 等（未実装）。</summary>
    Database = 1,

    /// <summary>KMS / Secret Manager 等（未実装）。</summary>
    KeyManagementService = 2,
}

/// <summary>notification builtin のプラットフォーム設定。</summary>
internal sealed class NotificationOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Notification";

    /// <summary>環境変数 <c>STATEVIA_NOTIFICATION_SMTP_SOURCE</c>。</summary>
    public const string SmtpSourceEnvironmentVariable = "STATEVIA_NOTIFICATION_SMTP_SOURCE";

    /// <summary>SMTP 接続設定の解決元。</summary>
    public NotificationSmtpSettingsSource SmtpSettingsSource { get; set; } = NotificationSmtpSettingsSource.Environment;

    /// <summary>Environment ソース向け SMTP 接続（appsettings。未設定時はレガシー env にフォールバック）。</summary>
    public SmtpConnectionOptions Smtp { get; set; } = new();
}

/// <summary>Environment ソースで bind する SMTP 接続オプション。</summary>
internal sealed class SmtpConnectionOptions
{
    /// <summary>SMTP ホスト。</summary>
    public string? Host { get; set; }

    /// <summary>SMTP ポート（既定 587）。</summary>
    public int Port { get; set; } = 587;

    /// <summary>認証ユーザー（任意）。</summary>
    public string? User { get; set; }

    /// <summary>認証パスワード（任意）。</summary>
    public string? Password { get; set; }

    /// <summary>既定 From アドレス（任意）。</summary>
    public string? DefaultFrom { get; set; }
}
