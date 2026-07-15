namespace Statevia.Infrastructure.Modules;

/// <summary>S3 互換オブジェクトストレージを取得元とする Action Module Source の設定。</summary>
/// <remarks>
/// <para>設定セクション <c>Statevia:Modules:S3</c>。<see cref="Enabled"/> が <see langword="false"/> の場合は
/// DI 登録されない（後方互換）。</para>
/// <para>セキュリティ: 認証情報（<see cref="S3ModuleArtifactOptions.SecretAccessKey"/> 等）は機密として扱い、
/// ログへ出力しない。bucket / key はサーバー側で必須検証する。</para>
/// </remarks>
internal sealed class S3ModuleSourceOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules:S3";

    /// <summary>S3 Source を有効化するか。未設定時は無効。</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 集約時の優先度（小さいほど優先）。既定は OCI（200）より低い 300 とし、
    /// 同名 Module ではローカル / OCI を優先する。
    /// </summary>
    public int Priority { get; set; } = 300;

    /// <summary>
    /// materialize 先のキャッシュルート絶対パス。未設定時は <c>{ContentRoot}/s3-modules-cache</c>。
    /// filesystem / OCI キャッシュとは分離し、二重 discover を避ける。
    /// </summary>
    public string? CacheRoot { get; set; }

    /// <summary>取得対象の S3 artifact 一覧（明示 key のみ。prefix 一覧は非目標）。</summary>
    public List<S3ModuleArtifactOptions> Artifacts { get; set; } = [];
}

/// <summary>取得する S3 artifact 1 件分の設定。</summary>
/// <remarks>
/// <see cref="AccessKeyId"/> / <see cref="SecretAccessKey"/> をいずれも設定しない場合は
/// デフォルト credential chain（環境変数・IAM ロール等）を用いる。
/// </remarks>
internal sealed class S3ModuleArtifactOptions
{
    /// <summary>バケット名。</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Module 配布 zip オブジェクトのキー。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>AWS リージョン（例: <c>ap-northeast-1</c>）。</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>カスタムエンドポイント（MinIO 等）。未設定時は AWS 既定。</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>アクセスキー（任意・機密）。</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>シークレットキー（任意・機密）。</summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>オブジェクト VersionId（任意。バージョニング有効バケット向け）。</summary>
    public string? VersionId { get; set; }
}
