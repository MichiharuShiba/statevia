using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// IEventProvider の実装。Wait/Resume のセマンティクスを提供します。
/// 各 Wait は一度だけ Resume（Publish）で再開できます。
/// </summary>
public sealed class EventProvider : IEventProvider
{
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _waiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// 指定ワークフローに紐づくイベントプロバイダを構築する（相関用の識別子を保持する）。
    /// </summary>
    /// <param name="workflowId">ワークフローインスタンス ID。</param>
    public EventProvider(string workflowId) { }

    /// <inheritdoc />
    public Task WaitAsync(string eventName, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        lock (_lock)
        {
            if (!_waiters.TryGetValue(eventName, out var list)) { list = []; _waiters[eventName] = list; }
            list.Add(tcs);
        }
        if (ct.CanBeCanceled)
        {
            ct.Register(() => tcs.TrySetCanceled(ct));
        }
        return tcs.Task;
    }

    /// <summary>指定イベントで待機中の Wait を全て再開します。</summary>
    public void Publish(string eventName)
    {
        lock (_lock)
        {
            if (_waiters.TryGetValue(eventName, out var list))
            {
                foreach (var tcs in list)
                {
                    tcs.TrySetResult(true);
                }
                _waiters.Remove(eventName);
            }
        }
    }
}
