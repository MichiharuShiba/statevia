namespace Statevia.Reference.Notification;

/// <summary>Notification リファレンス Module の識別子。</summary>
public static class NotificationReferenceActionIds
{
    /// <summary>ModuleId（filesystem 配置ディレクトリ名と一致させる）。</summary>
    public const string ModuleId = "statevia.action.reference.notification";

    /// <summary>email 送信 Action の canonical ID。</summary>
    public const string Send = ModuleId + ".send";
}
