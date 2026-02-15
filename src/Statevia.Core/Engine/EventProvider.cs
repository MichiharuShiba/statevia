using Statevia.Core.Abstractions;

namespace Statevia.Core.Engine;

/// <summary>
/// IEventProvider の実装。Wait/Resume のセマンティクスを提供します。
/// 各 Wait は一度だけ Resume（Publish）で再開できます。
/// </summary>
public sealed class EventProvider : IEventProvider
{
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _waiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public EventProvider(string workflowId) { }

    public Task WaitAsync(string eventName, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        lock (_lock)
        {
            if (!_waiters.TryGetValue(eventName, out var list)) { list = new List<TaskCompletionSource<bool>>(); _waiters[eventName] = list; }
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
