namespace Statevia.Service.Api.Configuration;

/// <summary>
/// Development 環境での初回管理者自動作成設定。
/// <see cref="Enabled"/> は <c>IsDevelopment()</c> のときのみ有効（本番では無視）。
/// </summary>
public sealed class DevAdminBootstrapOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Bootstrap:DevAdmin";

    /// <summary>Development で自動作成するか。</summary>
    public bool Enabled { get; set; }

    /// <summary>外部テナントキー。</summary>
    public string TenantKey { get; set; } = "default";

    /// <summary>ログイン ID（Users.Email 列。開発用の簡易 ID 可）。</summary>
    public string Email { get; set; } = "admin";

    /// <summary>平文パスワード（開発専用。ログに出力しないこと）。</summary>
    public string Password { get; set; } = "admin";

    /// <summary>Principal 表示名（未指定時は <see cref="Email"/>）。</summary>
    public string? DisplayName { get; set; } = "admin";
}
