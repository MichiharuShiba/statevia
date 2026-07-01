using System.Security.Cryptography;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary>Module 署名検証テスト用に、RSA 鍵生成と detached 署名ファイル生成を補助する。</summary>
internal static class ModuleSignatureTestHelper
{
    /// <summary>新しい RSA-2048 鍵を生成する。呼び出し側が Dispose する。</summary>
    public static RSA CreateSigningKey() => RSA.Create(2048);

    /// <summary>公開鍵（SubjectPublicKeyInfo）の SHA-256 フィンガープリント（16 進大文字）を返す。</summary>
    public static string ComputeFingerprint(RSA rsa)
    {
        var spki = rsa.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(spki));
    }

    /// <summary>
    /// entry DLL に対する <c>{moduleDirectoryName}.signature.json</c> を生成する。
    /// 署名は entry DLL バイト列の SHA-256 に対して付与する。
    /// </summary>
    public static void WriteSignatureFile(
        string entryAssemblyPath,
        RSA rsa,
        string algorithm = "RSA-SHA256",
        string? signerName = null,
        string? signatureBase64Override = null)
    {
        var moduleDirectory = Path.GetDirectoryName(entryAssemblyPath)!;
        var moduleDirectoryName = Path.GetFileName(moduleDirectory);
        var entryBytes = File.ReadAllBytes(entryAssemblyPath);

        var signatureBase64 = signatureBase64Override ?? Convert.ToBase64String(
            rsa.SignData(entryBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        var manifest = new
        {
            algorithm,
            publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            signatureBase64,
            signerName,
        };

        var signaturePath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
        File.WriteAllText(signaturePath, JsonSerializer.Serialize(manifest));
    }

    /// <summary>署名ファイルに任意の JSON 本文を直接書き込む（破損 manifest テスト用）。</summary>
    public static void WriteRawSignatureFile(string entryAssemblyPath, string rawJson)
    {
        var moduleDirectory = Path.GetDirectoryName(entryAssemblyPath)!;
        var moduleDirectoryName = Path.GetFileName(moduleDirectory);
        var signaturePath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
        File.WriteAllText(signaturePath, rawJson);
    }
}
