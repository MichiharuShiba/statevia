namespace Statevia.Core.Api.Application.Actions.Validation;

/// <summary>action input schema 検証の段階的厳格化オプション。</summary>
internal static class ActionInputSchemaValidationOptions
{
    /// <summary>
    /// Builtin 以外の action を compile error にするか。
    /// 既定は true（Module 由来 action は publication 必須）。false のとき Module も warning のみ。
    /// </summary>
    public static bool RequireSchemaForModuleActions { get; set; } = true;

    /// <summary>
    /// Publication 未登録 action を compile error にするか（Builtin カスタム登録向け）。
    /// 既定は false（structured warning ログのみ）。
    /// </summary>
    public static bool RequireSchemaForUnpublishedActions { get; set; }
}
