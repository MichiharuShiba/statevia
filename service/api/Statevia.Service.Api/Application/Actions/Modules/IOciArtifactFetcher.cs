namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>OCI artifact 取得の抽象。具体ライブラリ（ORAS 等）への依存を本契約に封じ込める。</summary>
/// <remarks>
/// 返却は「Module 配布 zip のレイヤ bytes ＋ manifest digest」のみとし、materialize（展開・entry 解決・
/// 署名検証）は呼び出し側（<see cref="OciModuleSource"/>）が担う。これにより取得ライブラリの破壊的変更の
/// 影響をアダプタ実装 1 箇所に限定する。
/// </remarks>
internal interface IOciArtifactFetcher
{
    /// <summary>指定 reference の Module artifact を pull し、配布 zip レイヤと manifest digest を返す。</summary>
    /// <param name="reference">取得対象の参照（registry / repository / tag or digest / 認証）。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>取得した Module artifact。</returns>
    Task<OciFetchedModule> FetchModuleAsync(OciModuleReference reference, CancellationToken cancellationToken);
}

/// <summary>OCI Module artifact の参照（取得元・認証）。</summary>
/// <param name="Registry">registry ホスト。</param>
/// <param name="Repository">repository。</param>
/// <param name="Reference">tag または digest。</param>
/// <param name="Username">basic 認証ユーザー名（任意・機密）。</param>
/// <param name="Password">basic 認証パスワード（任意・機密）。</param>
/// <param name="RefreshToken">refresh token（任意・機密）。</param>
/// <param name="PlainHttp">HTTP（非 TLS）接続を使うか。</param>
internal sealed record OciModuleReference(
    string Registry,
    string Repository,
    string Reference,
    string? Username,
    string? Password,
    string? RefreshToken,
    bool PlainHttp)
{
    /// <summary>可観測性用ラベル（<c>oci:{registry}/{repository}:{reference}</c>）。機密は含めない。</summary>
    public string Label => $"oci:{Registry}/{Repository}:{Reference}";
}

/// <summary>取得済み OCI Module artifact。</summary>
/// <param name="LayerZip">Module 配布 zip レイヤの bytes。</param>
/// <param name="ManifestDigest">manifest の digest（cache キー・可観測性に使用）。</param>
internal sealed record OciFetchedModule(byte[] LayerZip, string ManifestDigest);
