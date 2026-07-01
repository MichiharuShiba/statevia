namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>Module 署名検証の設定。</summary>
/// <remarks>
/// <para>設定セクションは <see cref="SectionName"/>（<c>Statevia:Modules:Signing</c>）。</para>
/// <para>
/// セキュリティ: <see cref="TrustedSignerFingerprints"/> は署名ファイルの公開鍵から再計算したフィンガープリントと
/// 照合する許可集合であり、これに含まれる署名者のみ <c>Verified</c> に昇格する。
/// </para>
/// </remarks>
internal sealed class ModuleSigningOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:Modules:Signing";

    /// <summary>
    /// 信頼する署名者の公開鍵フィンガープリント（SubjectPublicKeyInfo の SHA-256、16 進）。
    /// 大文字小文字・区切り文字は正規化して比較する。空のときは全署名が <c>Signed</c> 止まりになる。
    /// </summary>
    public IReadOnlyList<string> TrustedSignerFingerprints { get; set; } = [];

    /// <summary>
    /// true のとき、署名ファイルを持たない Module の登録を拒否（skip）する。
    /// 既定 false（署名なしは従来どおり <c>Community</c> として登録）。
    /// </summary>
    public bool RequireSignature { get; set; }
}
