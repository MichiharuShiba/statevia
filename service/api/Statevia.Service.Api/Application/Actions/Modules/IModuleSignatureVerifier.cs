namespace Statevia.Service.Api.Application.Actions.Modules;

/// <summary>Module の detached 署名を検証し、信頼レベルを決定する。</summary>
internal interface IModuleSignatureVerifier
{
    /// <summary>
    /// entry assembly に対応する署名ファイルを検証し、信頼レベルと署名メタデータを返す。
    /// </summary>
    /// <param name="entryAssemblyPath">entry DLL の絶対パス。</param>
    /// <returns>検証結果。検証例外時も fail-safe に <c>Untrusted</c> を返す（throw しない）。</returns>
    ModuleSignatureVerificationResult Verify(string entryAssemblyPath);
}
