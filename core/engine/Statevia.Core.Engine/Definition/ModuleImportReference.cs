namespace Statevia.Core.Engine.Definition;

/// <summary>
/// <c>workflow.modules</c> の import 宣言（syntax parse 済み・版解決前）。
/// </summary>
/// <remarks>
/// <para>Loader は semantic resolution を行わず、moduleId と versionRange を保持するのみ。</para>
/// <para>版の具体解決は Compiler 配下の VersionResolver が compile 時に 1 回だけ行う。</para>
/// </remarks>
/// <param name="ModuleId">参照先 Module の一意識別子。</param>
/// <param name="VersionRange">版レンジ式（例 <c>^1.2</c>）。空文字は最新安定版（LATEST）。</param>
public sealed record ModuleImportReference(string ModuleId, string VersionRange = "")
{
    /// <summary>YAML の import 値（例 <c>com.mail@^1.2</c>）を構造化する。</summary>
    /// <param name="importValue"><c>workflow.modules</c> の値。</param>
    /// <returns>解析結果。</returns>
    /// <exception cref="ArgumentException">import 値が空、または <c>@</c> 直後に ModuleId が無いとき。</exception>
    public static ModuleImportReference ParseImportValue(string importValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importValue);

        var trimmed = importValue.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex < 0)
        {
            return new ModuleImportReference(trimmed, VersionRange: string.Empty);
        }

        var moduleId = trimmed[..atIndex].Trim();
        var versionRange = trimmed[(atIndex + 1)..].Trim();
        if (moduleId.Length == 0)
        {
            throw new ArgumentException("workflow.modules import value requires a non-empty ModuleId.");
        }

        return new ModuleImportReference(moduleId, versionRange);
    }
}
