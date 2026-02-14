namespace Statevia.Core.Scheduler;

/// <summary>
/// 状態実行タスクをスケジュールするインターフェース。
/// 並列数制限などを行い、ワークフローエンジンから状態実行を委譲されます。
/// </summary>
public interface IScheduler
{
    /// <summary>指定した作業をスケジュールして実行し、結果を返します。</summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
