using Statevia.Actions.Abstractions.Catalog;

namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>Module 署名検証の結果。</summary>
/// <remarks>
/// <para>
/// 登録時の信頼レベルと、検証に成功した署名のメタデータを表す。<see cref="RejectRegistration"/> が true の場合、
/// 呼び出し側（<c>ModuleHost</c>）は登録を skip する（<c>RequireSignature</c> かつ署名なしのケース）。
/// </para>
/// </remarks>
/// <param name="TrustLevel">決定した信頼レベル。</param>
/// <param name="Signature">検証済み署名メタデータ（<c>Community</c> / <c>Untrusted</c> では null）。</param>
/// <param name="RejectRegistration">true のとき登録を拒否（skip）すべき。</param>
/// <param name="ReasonCategory">ログ用の理由カテゴリ（機密を含まない短い分類値）。</param>
internal sealed record ModuleSignatureVerificationResult(
    ActionTrustLevel TrustLevel,
    ActionSignature? Signature,
    bool RejectRegistration,
    string ReasonCategory);
