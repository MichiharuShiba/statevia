using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Catalog;

namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>
/// module ディレクトリ直下の detached 署名ファイルを検証し、<see cref="ActionTrustLevel"/> を決定する。
/// </summary>
/// <remarks>
/// <para>
/// 署名ファイルは <c>{moduleDirectoryName}.signature.json</c>（entry DLL の兄弟）。entry DLL バイト列の
/// SHA-256 への署名を、署名ファイル同梱の公開鍵（PEM）で検証する。信頼判定は公開鍵から再計算した
/// フィンガープリント（SubjectPublicKeyInfo の SHA-256）と許可集合の照合のみで行う（manifest 申告値は使わない）。
/// </para>
/// <para>セキュリティ: 検証中の例外は握りつぶして fail-safe に <c>Untrusted</c> を返す。公開鍵・署名本体はログに出さない。</para>
/// </remarks>
internal sealed class ModuleSignatureVerifier : IModuleSignatureVerifier
{
    /// <summary>MVP で対応する唯一の署名アルゴリズム。</summary>
    private const string SupportedAlgorithm = "RSA-SHA256";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ModuleSigningOptions _options;
    private readonly HashSet<string> _trustedFingerprints;
    private readonly ILogger<ModuleSignatureVerifier> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ModuleSignatureVerifier(
        IOptions<ModuleSigningOptions> options,
        ILogger<ModuleSignatureVerifier> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _trustedFingerprints = _options.TrustedSignerFingerprints
            .Select(NormalizeFingerprint)
            .Where(fingerprint => fingerprint.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ModuleSignatureVerificationResult Verify(string entryAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

        var signaturePath = ResolveSignaturePath(entryAssemblyPath);
        if (signaturePath is null || !File.Exists(signaturePath))
        {
            return _options.RequireSignature
                ? new ModuleSignatureVerificationResult(
                    ActionTrustLevel.Untrusted, Signature: null, RejectRegistration: true, "signature-required")
                : new ModuleSignatureVerificationResult(
                    ActionTrustLevel.Community, Signature: null, RejectRegistration: false, "no-signature");
        }

        try
        {
            return VerifyCore(entryAssemblyPath, signaturePath);
        }
        catch (Exception ex) when (ex is CryptographicException
            or FormatException
            or JsonException
            or IOException
            or ArgumentException
            or NotSupportedException)
        {
            ModuleSignatureVerifierLog.VerificationError(_logger, ex, "verification-error");
            return Untrusted("verification-error");
        }
    }

    private ModuleSignatureVerificationResult VerifyCore(string entryAssemblyPath, string signaturePath)
    {
        var manifestJson = File.ReadAllText(signaturePath);
        var manifest = JsonSerializer.Deserialize<SignatureManifest>(manifestJson, ManifestJsonOptions);

        if (manifest is null
            || string.IsNullOrWhiteSpace(manifest.Algorithm)
            || string.IsNullOrWhiteSpace(manifest.PublicKeyPem)
            || string.IsNullOrWhiteSpace(manifest.SignatureBase64))
        {
            return Untrusted("malformed-manifest");
        }

        if (!string.Equals(manifest.Algorithm.Trim(), SupportedAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            return Untrusted("unsupported-algorithm");
        }

        if (!TryDecodeBase64(manifest.SignatureBase64, out var signatureBytes))
        {
            return Untrusted("malformed-manifest");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(manifest.PublicKeyPem);

        var entryBytes = File.ReadAllBytes(entryAssemblyPath);
        var isValidSignature = rsa.VerifyData(
            entryBytes,
            signatureBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (!isValidSignature)
        {
            return Untrusted("invalid-signature");
        }

        var fingerprint = ComputeFingerprint(rsa);
        var signerName = string.IsNullOrWhiteSpace(manifest.SignerName) ? null : manifest.SignerName.Trim();
        var signature = new ActionSignature(
            SupportedAlgorithm,
            fingerprint,
            signerName,
            DateTimeOffset.UtcNow);

        if (_trustedFingerprints.Contains(fingerprint))
        {
            return new ModuleSignatureVerificationResult(
                ActionTrustLevel.Verified, signature, RejectRegistration: false, "verified");
        }

        return new ModuleSignatureVerificationResult(
            ActionTrustLevel.Signed, signature, RejectRegistration: false, "signed-untrusted-signer");
    }

    /// <summary>entry DLL の兄弟として <c>{moduleDirectoryName}.signature.json</c> を解決する。</summary>
    private static string? ResolveSignaturePath(string entryAssemblyPath)
    {
        var moduleDirectory = Path.GetDirectoryName(entryAssemblyPath);
        if (string.IsNullOrEmpty(moduleDirectory))
        {
            return null;
        }

        var moduleDirectoryName = Path.GetFileName(moduleDirectory);
        return string.IsNullOrEmpty(moduleDirectoryName)
            ? null
            : Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
    }

    /// <summary>公開鍵（SubjectPublicKeyInfo）の SHA-256 フィンガープリントを 16 進大文字で返す。</summary>
    private static string ComputeFingerprint(RSA rsa)
    {
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(spki);
        return Convert.ToHexString(hash);
    }

    /// <summary>区切り文字・空白を除去し大文字化したフィンガープリント正規形を返す。</summary>
    private static string NormalizeFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(static character => Uri.IsHexDigit(character))
            .ToArray())
            .ToUpperInvariant();
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static ModuleSignatureVerificationResult Untrusted(string reasonCategory) =>
        new(ActionTrustLevel.Untrusted, Signature: null, RejectRegistration: false, reasonCategory);

    /// <summary>署名ファイルの逆シリアライズ用 DTO。</summary>
    private sealed record SignatureManifest(
        [property: JsonPropertyName("algorithm")] string? Algorithm,
        [property: JsonPropertyName("publicKeyPem")] string? PublicKeyPem,
        [property: JsonPropertyName("signatureBase64")] string? SignatureBase64,
        [property: JsonPropertyName("signerName")] string? SignerName);
}

internal static partial class ModuleSignatureVerifierLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Module signature verification error ({ReasonCategory})")]
    public static partial void VerificationError(ILogger logger, Exception exception, string reasonCategory);
}
