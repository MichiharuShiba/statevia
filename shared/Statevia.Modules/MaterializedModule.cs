namespace Statevia.Modules;

/// <summary>取得元に依らず ModuleHost がそのまま load できる、Module のローカル完全表現（正本）。</summary>
/// <remarks>
/// <para>
/// ModuleSource が「acquire → cache → verify → extract → materialize」を経て生成する成果物。
/// Filesystem / OCI など取得経路が異なっても、本表現に正規化することで ModuleHost の load 手順を共通化する。
/// </para>
/// <para>
/// 役割の区別: <see cref="MaterializedModule"/> は <b>ローカルに実体化された正本</b>であり、
/// ModuleHost / Source 間で受け渡す DTO（兼 Catalog 登録モデル）とは責務が異なる
/// （DTO 側は Core-API 内部の発見結果表現）。
/// </para>
/// <para>セキュリティ: <see cref="SignaturePath"/> は署名検証（<c>ModuleSignatureVerifier</c>）の入力であり、
/// 検証は materialize 後の filesystem に対して行う。</para>
/// </remarks>
public sealed record MaterializedModule
{
    /// <summary>Module ディレクトリの絶対パス。</summary>
    public required string ModuleDirectory { get; init; }

    /// <summary>entry DLL の絶対パス。</summary>
    public required string EntryAssemblyPath { get; init; }

    /// <summary>署名ファイル（<c>signature.json</c> 等）の絶対パス。署名なし Module では <see langword="null"/>。</summary>
    public string? SignaturePath { get; init; }

    /// <summary>manifest 由来の Module ID（load 前に判明する場合のみ。未確定は <see langword="null"/>）。</summary>
    public string? ModuleId { get; init; }

    /// <summary>manifest 由来の Module バージョン（load 前に判明する場合のみ。未確定は <see langword="null"/>）。</summary>
    public string? Version { get; init; }

    /// <summary>取得元の種別（例: <c>filesystem</c> / <c>oci</c>）。将来拡張枠（MVP 必須ではない）。</summary>
    public string? SourceType { get; init; }

    /// <summary>取得元ラベル（例: <c>oci:{ref}</c>）。可観測性・重複解決用。将来拡張枠（MVP 必須ではない）。</summary>
    public string? SourceLabel { get; init; }

    /// <summary>取得コンテンツのダイジェスト（cache hit 判定用）。将来拡張枠（MVP 必須ではない）。</summary>
    public string? ContentDigest { get; init; }

    /// <summary>materialize した時刻。将来拡張枠（MVP 必須ではない）。</summary>
    public DateTimeOffset? MaterializedAt { get; init; }
}
