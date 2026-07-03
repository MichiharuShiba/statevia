namespace Statevia.Infrastructure.Modules;

/// <summary>OCI registry を取得元とする Action Module Source の設定。</summary>
/// <remarks>
/// <para>設定セクション <c>Statevia:Modules:Oci</c>。<see cref="Enabled"/> が <see langword="false"/> の場合は
/// DI 登録されず、従来どおり filesystem Source のみが有効となる（後方互換）。</para>
/// <para>セキュリティ: 認証情報（<see cref="OciModuleArtifactOptions.Password"/> 等）は機密として扱い、
/// ログへ出力しない。registry / repository / reference はサーバー側で必須検証する。</para>
/// </remarks>
internal sealed class OciModuleSourceOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules:Oci";

    /// <summary>OCI Source を有効化するか。未設定時は無効（filesystem のみ）。</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 集約時の優先度（小さいほど優先）。既定は filesystem（100）より低い 200 とし、
    /// 同名 Module ではローカル配置を優先する。運用で remote を優先したい場合に下げる。
    /// </summary>
    public int Priority { get; set; } = 200;

    /// <summary>
    /// materialize 先のキャッシュルート絶対パス。未設定時は <c>{ContentRoot}/oci-modules-cache</c>。
    /// filesystem modules ルートとは分離し、二重 discover を避ける。
    /// </summary>
    public string? CacheRoot { get; set; }

    /// <summary>取得対象の OCI artifact 一覧。</summary>
    public List<OciModuleArtifactOptions> Artifacts { get; set; } = [];
}

/// <summary>取得する OCI artifact 1 件分の設定。</summary>
/// <remarks>
/// 認証情報（<see cref="Username"/> / <see cref="Password"/> / <see cref="RefreshToken"/>）を
/// いずれも設定しない場合は匿名 pull となる（公開レジストリ向け）。
/// </remarks>
internal sealed class OciModuleArtifactOptions
{
    /// <summary>registry ホスト（例: <c>ghcr.io</c> / <c>localhost:5000</c>）。</summary>
    public string Registry { get; set; } = string.Empty;

    /// <summary>repository（例: <c>myorg/order.module</c>）。</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>tag または digest（例: <c>1.0.0</c> / <c>sha256:...</c>）。</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>basic 認証ユーザー名（任意）。</summary>
    public string? Username { get; set; }

    /// <summary>basic 認証パスワード（任意・機密）。</summary>
    public string? Password { get; set; }

    /// <summary>refresh token（任意・機密）。</summary>
    public string? RefreshToken { get; set; }

    /// <summary>HTTP（非 TLS）で接続するか。ローカル registry 検証用途のみ。既定は <see langword="false"/>。</summary>
    public bool PlainHttp { get; set; }
}
