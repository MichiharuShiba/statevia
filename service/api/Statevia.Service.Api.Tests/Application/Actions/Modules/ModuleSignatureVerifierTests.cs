using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleSignatureVerifier"/> の TrustLevel 判定テスト。</summary>
public sealed class ModuleSignatureVerifierTests
{
    /// <summary>署名ファイルなし・RequireSignature=false は Community（登録継続）。</summary>
    [Fact]
    public void Verify_WhenNoSignatureAndNotRequired_ReturnsCommunity()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        var verifier = CreateVerifier();

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Community, result.TrustLevel);
        Assert.Null(result.Signature);
        Assert.False(result.RejectRegistration);
    }

    /// <summary>署名ファイルなし・RequireSignature=true は登録拒否。</summary>
    [Fact]
    public void Verify_WhenNoSignatureAndRequired_RejectsRegistration()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        var verifier = CreateVerifier(new ModuleSigningOptions { RequireSignature = true });

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.True(result.RejectRegistration);
        Assert.Null(result.Signature);
    }

    /// <summary>有効署名かつフィンガープリントが許可集合内なら Verified。</summary>
    [Fact]
    public void Verify_WhenValidSignatureAndTrustedFingerprint_ReturnsVerified()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa, signerName: "Statevia Official");
        var fingerprint = ModuleSignatureTestHelper.ComputeFingerprint(rsa);
        var verifier = CreateVerifier(new ModuleSigningOptions
        {
            TrustedSignerFingerprints = [fingerprint],
        });

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Verified, result.TrustLevel);
        Assert.NotNull(result.Signature);
        Assert.Equal(fingerprint, result.Signature!.SignerFingerprint);
        Assert.Equal("Statevia Official", result.Signature.SignerName);
    }

    /// <summary>有効署名だがフィンガープリントが許可集合外なら Signed。</summary>
    [Fact]
    public void Verify_WhenValidSignatureButUntrustedFingerprint_ReturnsSigned()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa);
        var verifier = CreateVerifier(new ModuleSigningOptions
        {
            TrustedSignerFingerprints = ["00DEADBEEF"],
        });

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Signed, result.TrustLevel);
        Assert.NotNull(result.Signature);
        Assert.False(result.RejectRegistration);
    }

    /// <summary>署名後に entry DLL が改ざんされた場合は Untrusted。</summary>
    [Fact]
    public void Verify_WhenEntryTamperedAfterSigning_ReturnsUntrusted()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa);
        File.AppendAllText(entryPath, "tampered");
        var verifier = CreateVerifier(new ModuleSigningOptions
        {
            TrustedSignerFingerprints = [ModuleSignatureTestHelper.ComputeFingerprint(rsa)],
        });

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Untrusted, result.TrustLevel);
        Assert.Null(result.Signature);
    }

    /// <summary>未対応アルゴリズムは Untrusted。</summary>
    [Fact]
    public void Verify_WhenUnsupportedAlgorithm_ReturnsUntrusted()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa, algorithm: "ECDSA-SHA512");
        var verifier = CreateVerifier();

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Untrusted, result.TrustLevel);
    }

    /// <summary>破損した manifest は Untrusted。</summary>
    [Fact]
    public void Verify_WhenMalformedManifest_ReturnsUntrusted()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        ModuleSignatureTestHelper.WriteRawSignatureFile(entryPath, "{ not valid json ");
        var verifier = CreateVerifier();

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Untrusted, result.TrustLevel);
    }

    /// <summary>JSON は妥当だが必須フィールドが欠落した manifest は Untrusted。</summary>
    [Fact]
    public void Verify_WhenManifestMissingRequiredFields_ReturnsUntrusted()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        ModuleSignatureTestHelper.WriteRawSignatureFile(
            entryPath,
            "{\"algorithm\":\"RSA-SHA256\",\"publicKeyPem\":\"\",\"signatureBase64\":\"\"}");
        var verifier = CreateVerifier();

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Untrusted, result.TrustLevel);
        Assert.Null(result.Signature);
    }

    /// <summary>署名値が Base64 として不正な manifest は Untrusted。</summary>
    [Fact]
    public void Verify_WhenSignatureNotBase64_ReturnsUntrusted()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(
            entryPath,
            rsa,
            signatureBase64Override: "not valid base64 @@@");
        var verifier = CreateVerifier();

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Untrusted, result.TrustLevel);
        Assert.Null(result.Signature);
    }

    /// <summary>許可集合に空白だけの要素があっても無視され、未許可署名は Signed のまま。</summary>
    [Fact]
    public void Verify_WhenTrustedFingerprintsContainBlankEntry_IgnoresBlankAndReturnsSigned()
    {
        // Arrange
        var entryPath = CreateEntryAssembly("test.module");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa);
        var verifier = CreateVerifier(new ModuleSigningOptions
        {
            TrustedSignerFingerprints = ["   "],
        });

        // Act
        var result = verifier.Verify(entryPath);

        // Assert
        Assert.Equal(ActionTrustLevel.Signed, result.TrustLevel);
        Assert.NotNull(result.Signature);
    }

    private static ModuleSignatureVerifier CreateVerifier(ModuleSigningOptions? options = null) =>
        new(
            Options.Create(options ?? new ModuleSigningOptions()),
            NullLogger<ModuleSignatureVerifier>.Instance);

    private static string CreateEntryAssembly(string moduleDirectoryName)
    {
        var root = Path.Combine(Path.GetTempPath(), "statevia-signature-test", Guid.NewGuid().ToString("N"));
        var moduleDirectory = Path.Combine(root, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);
        var entryPath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        File.WriteAllBytes(entryPath, [0x4D, 0x5A, 0x90, 0x00, 0x01, 0x02, 0x03, 0x04]);
        return entryPath;
    }
}
