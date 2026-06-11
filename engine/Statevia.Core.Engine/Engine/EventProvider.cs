using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="IEventProvider"/> の実装。Wait/Resume のセマンティクスを提供します。
/// 各 Wait は一度だけ Resume（Signal / Publish）で再開できます。
/// </summary>
public sealed partial class EventProvider : IEventProvider
{
    private readonly string _executionId;
    private readonly Dictionary<string, List<TaskCompletionSource<bool>>> _waiters = new(StringComparer.OrdinalIgnoreCase);
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
    public Task WaitForSignalAsync(string signalName, CancellationToken ct) =>
        WaitAsync(signalName, ct);

    /// <inheritdoc />
    public void Signal(string signalName) => Publish(signalName);

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
}
