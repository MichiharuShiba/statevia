namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary><c>workflow.modules</c> の import 文字列（<c>moduleId@range</c>）を解析する。</summary>
internal static class ModuleImportParser
{
    /// <summary>import 文字列を <see cref="ModuleReference"/> に変換する。</summary>
    /// <param name="alias">定義内 alias。</param>
    /// <param name="importValue"><c>workflow.modules</c> の値（例 <c>com.mail@^1.2</c>）。</param>
    /// <returns>解析結果。</returns>
    /// <exception cref="ArgumentException">import 値が空のとき。</exception>
    public static ModuleReference Parse(string alias, string importValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(importValue);

        var trimmed = importValue.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex < 0)
        {
            return new ModuleReference(alias.Trim(), trimmed, VersionRange: string.Empty);
        }

        var moduleId = trimmed[..atIndex].Trim();
        var versionRange = trimmed[(atIndex + 1)..].Trim();
        if (moduleId.Length == 0)
        {
            throw new ArgumentException($"workflow.modules['{alias}'] requires a non-empty ModuleId.");
        }

        return new ModuleReference(alias.Trim(), moduleId, versionRange);
    }
}
