using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="IEventProvider"/> の実装。Wait/Resume のセマンティクスを提供します。
/// 各 Wait は一度だけ Resume（ノードスコープ / Signal / Publish）で再開できます。
/// </summary>
public sealed partial class EventProvider : IEventProvider
{
    private readonly string _executionId;
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _waiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NodeWaitRegistration> _nodeWaiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ILogger _logger;

    /// <summary>
    /// 指定ワークフローに紐づくイベントプロバイダを構築する（相関用の識別子を保持する）。
    /// </summary>
    /// <param name="executionId">ワークフローインスタンス ID。</param>
    /// <param name="logger">トピック発行ログ用（省略時は Null）。</param>
    public EventProvider(string executionId, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        _executionId = executionId;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public Task WaitAsync(string eventName, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        lock (_lock)
        {
            if (!_waiters.TryGetValue(eventName, out var list))
            {
                list = [];
                _waiters[eventName] = list;
            }

            list.Add(tcs);
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() => tcs.TrySetCanceled(ct));
        }

        return tcs.Task;
    }

    /// <inheritdoc />
    public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(eventNames);
        if (eventNames.Count == 0)
        {
            throw new ArgumentException("eventNames must not be empty.", nameof(eventNames));
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in eventNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("eventNames must not contain null or whitespace.", nameof(eventNames));
            }

            allowed.Add(name.Trim());
        }

        if (allowed.Count == 0)
        {
            throw new ArgumentException("eventNames must not be empty.", nameof(eventNames));
        }

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (_nodeWaiters.ContainsKey(nodeId))
            {
                throw new InvalidOperationException(
                    $"Node '{nodeId}' already has an active wait (1 Wait = 1 resume).");
            }

            _nodeWaiters[nodeId] = new NodeWaitRegistration(allowed, tcs);
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                lock (_lock)
                {
                    if (_nodeWaiters.TryGetValue(nodeId, out var registration)
                        && ReferenceEquals(registration.Completion, tcs))
                    {
                        _nodeWaiters.Remove(nodeId);
                    }
                }

                tcs.TrySetCanceled(ct);
            });
        }

        return tcs.Task;
    }

    /// <inheritdoc />
    public Task WaitForSignalAsync(string signalName, CancellationToken ct) =>
        WaitAsync(signalName, ct);

    /// <inheritdoc />
    public void Signal(string signalName) => Publish(signalName);

    /// <inheritdoc />
    public void Resume(string nodeId, string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var trimmedEventName = eventName.Trim();
        TaskCompletionSource<string>? completion = null;
        lock (_lock)
        {
            if (!_nodeWaiters.TryGetValue(nodeId, out var registration))
            {
                return;
            }

            if (!registration.AllowedEvents.Contains(trimmedEventName))
            {
                return;
            }

            // 1 Wait = 1 回再開: 未使用イベント分の waiter もまとめて除去する。
            _nodeWaiters.Remove(nodeId);
            completion = registration.Completion;
        }

        completion.TrySetResult(trimmedEventName);
    }

    /// <inheritdoc />
    public void PublishTopic(string topic, object? payloadSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        EventProviderLog.DomainEventDispatched(
            _logger,
            _executionId,
            topic,
            payloadSummary?.GetType().Name ?? "null");
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

    /// <summary>ノード単位のイベント待機登録。</summary>
    /// <param name="AllowedEvents">受付イベント名集合。</param>
    /// <param name="Completion">Resume 時に完了する TCS。</param>
    private sealed record NodeWaitRegistration(
        HashSet<string> AllowedEvents,
        TaskCompletionSource<string> Completion);
}
