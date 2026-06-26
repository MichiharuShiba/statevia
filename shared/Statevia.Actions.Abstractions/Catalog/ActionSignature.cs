namespace Statevia.Actions.Abstractions.Catalog;

/// <summary>Module 署名の検証済みメタデータ。</summary>
/// <remarks>
/// <para>
/// 署名検証パイプライン（Phase 3）が Module 登録時に生成する。検証に成功した署名（<c>Verified</c> / <c>Signed</c>）の
/// 出所情報を表す。生署名本体や証明書サムプリントは保持しない（検証後は不要で、誤露出を避けるため）。
/// </para>
/// <para>
/// <see cref="SignerName"/> は署名ファイル由来の自己申告値であり、信頼判定には使用しない（表示専用）。
/// 信頼判定は <see cref="SignerFingerprint"/>（公開鍵から再計算）と許可集合の照合のみで行う。
/// </para>
/// </remarks>
/// <param name="Algorithm">署名アルゴリズム（例 <c>RSA-SHA256</c>）。</param>
/// <param name="SignerFingerprint">署名者公開鍵（SubjectPublicKeyInfo）の SHA-256 フィンガープリント（16 進）。信頼判定の唯一の根拠。</param>
/// <param name="SignerName">署名者の表示名。自己申告・表示専用で信頼判定に使わない。未指定なら null。</param>
/// <param name="VerifiedUtc">署名生成日時ではなく、ホストが検証を実施した UTC 時刻。</param>
public sealed record ActionSignature(
    string Algorithm,
    string SignerFingerprint,
    string? SignerName,
    DateTimeOffset VerifiedUtc);
