namespace Statevia.Infrastructure.Modules;

/// <summary>S3 artifact 取得の抽象。具体 SDK（AWSSDK.S3）への依存を本契約に封じ込める。</summary>
/// <remarks>
/// <para>
/// 返却は「Module 配布 zip bytes ＋ content identity」のみとし、materialize は呼び出し側
/// （<see cref="S3ModuleSource"/>）が担う。
/// </para>
/// <para>
/// <see cref="ResolveContentIdentityAsync"/> はオブジェクト本体を取得せず identity（ETag / VersionId）のみ返す。
/// 呼び出し側がキャッシュ命中判定に使い、命中時は <see cref="FetchModuleAsync"/> を省略できる。
/// </para>
/// </remarks>
internal interface IS3ArtifactFetcher
{
    /// <summary>
    /// 指定オブジェクトの content identity を解決する（本体 blob は取得しない）。
    /// </summary>
    /// <param name="reference">取得対象の参照。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>キャッシュキー用 content identity。</returns>
    Task<string> ResolveContentIdentityAsync(S3ModuleReference reference, CancellationToken cancellationToken);

    /// <summary>指定オブジェクトを取得し、配布 zip と content identity を返す。</summary>
    /// <param name="reference">取得対象の参照。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>取得した Module artifact。</returns>
    Task<S3FetchedModule> FetchModuleAsync(S3ModuleReference reference, CancellationToken cancellationToken);
}

/// <summary>S3 Module artifact の参照（取得元・認証）。</summary>
/// <param name="Bucket">バケット名。</param>
/// <param name="Key">オブジェクトキー。</param>
/// <param name="Region">リージョン。</param>
/// <param name="ServiceUrl">カスタムエンドポイント（任意）。</param>
/// <param name="AccessKeyId">アクセスキー（任意・機密）。</param>
/// <param name="SecretAccessKey">シークレットキー（任意・機密）。</param>
/// <param name="VersionId">VersionId（任意）。</param>
internal sealed record S3ModuleReference(
    string Bucket,
    string Key,
    string Region,
    string? ServiceUrl,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? VersionId)
{
    /// <summary>可観測性用ラベル（機密は含めない）。</summary>
    public string Label =>
        string.IsNullOrEmpty(VersionId)
            ? $"s3:{Bucket}/{Key}"
            : $"s3:{Bucket}/{Key}?versionId={VersionId}";
}

/// <summary>取得済み S3 Module artifact。</summary>
/// <param name="ZipBytes">Module 配布 zip の bytes。</param>
/// <param name="ContentIdentity">content identity（cache キー・可観測性）。</param>
internal sealed record S3FetchedModule(byte[] ZipBytes, string ContentIdentity);
