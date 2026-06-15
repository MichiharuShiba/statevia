namespace Statevia.Core.Api.Application.Actions.Validation;

/// <summary>action input schema 検証の段階的厳格化オプション。</summary>
internal static class ActionInputSchemaValidationOptions
{
    /// <summary>
    /// Publication 未登録 action を compile error にするか。
    /// 既定は false（structured warning ログのみ）。
    /// </summary>
    public static bool RequireSchemaForUnpublishedActions { get; set; }
}
