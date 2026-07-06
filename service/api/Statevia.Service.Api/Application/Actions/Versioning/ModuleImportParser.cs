using Statevia.Core.Engine.Definition;

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

        var import = ModuleImportReference.ParseImportValue(importValue);
        return new ModuleReference(alias.Trim(), import.ModuleId, import.VersionRange);
    }

    /// <summary>構造化済み import を <see cref="ModuleReference"/> に変換する。</summary>
    /// <param name="alias">定義内 alias。</param>
    /// <param name="import">syntax parse 済み import。</param>
    public static ModuleReference Parse(string alias, ModuleImportReference import)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentNullException.ThrowIfNull(import);

        return new ModuleReference(alias.Trim(), import.ModuleId, import.VersionRange);
    }
}
