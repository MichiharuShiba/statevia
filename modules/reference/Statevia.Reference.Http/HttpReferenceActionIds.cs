namespace Statevia.Reference.Http;

/// <summary>HTTP リファレンス Module の識別子。</summary>
public static class HttpReferenceActionIds
{
    /// <summary>ModuleId（filesystem 配置ディレクトリ名と一致させる）。</summary>
    public const string ModuleId = "statevia.action.reference.http";

    /// <summary>HTTPS リクエスト Action の canonical ID。</summary>
    public const string Request = ModuleId + ".request";
}
