namespace Statevia.Actions.Abstractions.Catalog;

/// <summary>Action / Module の署名メタデータ（Phase 2〜）。</summary>
/// <param name="Algorithm">署名アルゴリズム。</param>
/// <param name="CertificateThumbprint">証明書サムプリント。</param>
/// <param name="SignatureBase64">署名本体（Base64、任意）。</param>
public sealed record ActionSignature(
    string Algorithm,
    string CertificateThumbprint,
    string? SignatureBase64);
