namespace Statevia.Infrastructure.Modules;

/// <summary>Git ホストを取得元とする Action Module Source の設定。</summary>
/// <remarks>
/// <para>設定セクション <c>Statevia:Modules:Git</c>。<see cref="Enabled"/> が <see langword="false"/> の場合は
/// DI 登録されない（後方互換）。</para>
/// <para>セキュリティ: <see cref="GitModuleArtifactOptions.Token"/> は機密として扱い、ログへ出力しない。</para>
/// </remarks>
internal sealed class GitModuleSourceOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules:Git";

    /// <summary>Git Source を有効化するか。未設定時は無効。</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 集約時の優先度（小さいほど優先）。既定は S3（300）より低い 400 とし、
    /// 同名 Module ではローカル / OCI / S3 を優先する。
    /// </summary>
    public int Priority { get; set; } = 400;

    /// <summary>
    /// materialize 先のキャッシュルート絶対パス。未設定時は <c>{ContentRoot}/git-modules-cache</c>。
    /// </summary>
    public string? CacheRoot { get; set; }

    /// <summary>取得対象の Git artifact 一覧。</summary>
    public List<GitModuleArtifactOptions> Artifacts { get; set; } = [];
}

/// <summary>取得する Git artifact 1 件分の設定。</summary>
internal sealed class GitModuleArtifactOptions
{
    /// <summary>ホスト（例: <c>github.com</c> / <c>gitlab.com</c> / self-hosted）。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>オーナーまたはグループ。</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>リポジトリ名。</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>branch / tag / commit SHA。</summary>
    public string Ref { get; set; } = string.Empty;

    /// <summary>
    /// archive 内の Module パス（リポジトリルート相対）。
    /// module ディレクトリ、または配布 <c>.zip</c> ファイルを指定する。
    /// </summary>
    public string ModulePath { get; set; } = string.Empty;

    /// <summary>
    /// ホスト種別（<c>github</c> / <c>gitlab</c>）。
    /// 未設定時は既知 Host のみ推定（<c>github.com</c> / <c>gitlab.com</c>）。
    /// self-hosted / Enterprise では必須。
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>認証トークン（任意・機密）。</summary>
    public string? Token { get; set; }

    /// <summary>HTTP（非 TLS）で接続するか。ローカル検証用途のみ。既定は <see langword="false"/>。</summary>
    public bool PlainHttp { get; set; }
}
