namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// ワークフロー投影更新の非同期キュー。
/// </summary>
public interface IExecutionProjectionUpdateQueue
{
    /// <summary>
    /// 指定ワークフローの投影更新をキューへ投入する。
    /// </summary>
    Task EnqueueAsync(Guid executionId, CancellationToken ct);

    /// <summary>
    /// 指定ワークフローに対する未処理更新が無くなるまで待機する。
    /// </summary>
    Task DrainAsync(Guid executionId, CancellationToken ct);
}
