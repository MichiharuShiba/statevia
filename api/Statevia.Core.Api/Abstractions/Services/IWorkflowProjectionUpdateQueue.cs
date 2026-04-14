namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// ワークフロー投影更新の非同期キュー。
/// </summary>
public interface IWorkflowProjectionUpdateQueue
{
    /// <summary>
    /// 指定ワークフローの投影更新をキューへ投入する。
    /// </summary>
    Task EnqueueAsync(Guid workflowId, CancellationToken ct);

    /// <summary>
    /// 指定ワークフローに対する未処理更新が無くなるまで待機する。
    /// </summary>
    Task DrainAsync(Guid workflowId, CancellationToken ct);
}
