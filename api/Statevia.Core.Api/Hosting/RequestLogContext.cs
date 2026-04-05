namespace Statevia.Core.Api.Hosting;

/// <summary>
/// リクエストログ用の <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> キー。
/// </summary>
public static class RequestLogContext
{
    /// <summary>ミドルウェアが決定した相関用 ID（後続の enrich やログで共有）。</summary>
    public const string TraceIdItemKey = "Statevia.TraceId";

    /// <summary>ルート <c>{id}</c> から取得したワークフロー表示 ID。</summary>
    public const string WorkflowDisplayIdItemKey = "Statevia.WorkflowDisplayId";

    /// <summary>ルート <c>{id}</c> から取得した定義表示 ID。</summary>
    public const string DefinitionDisplayIdItemKey = "Statevia.DefinitionDisplayId";

    /// <summary>ルート <c>{graphId}</c> から取得したグラフ定義 ID。</summary>
    public const string GraphDefinitionIdItemKey = "Statevia.GraphDefinitionId";
}
