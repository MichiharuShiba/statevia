namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="DefinitionService"/> の検証・NotFound メッセージ。
/// </summary>
internal static class DefinitionValidationMessages
{
    public const string NameRequired = "Definition name is required.";

    public const string YamlRequired = "Definition YAML is required.";

    /// <summary>コンパイル／検証失敗時の API エンベロープ要約メッセージ。</summary>
    public const string ValidationFailed = "Definition validation failed.";

    public const string NotFound = "Definition not found";
}
