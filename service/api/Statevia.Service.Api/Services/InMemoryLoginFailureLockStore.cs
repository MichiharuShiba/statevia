using System.Collections.Concurrent;

namespace Statevia.Service.Api.Services;

/// <summary>プロセス内メモリのログイン失敗ロック。</summary>
/// <remarks>
/// テナント＋ユーザー単位。失敗 10 回 / 15 分窓で 15 分ロックする。表は持たない。
/// 未知のテナント／ユーザーは呼び出し側で記録しない。保持件数は上限を超えず、超過時は未ロック主体を追い出す。
/// 複数 API インスタンスとプロセス再起動ではカウントが分かれる。
/// </remarks>
internal sealed class InMemoryLoginFailureLockStore : ILoginFailureLockStore
{
    /// <summary>ロックに必要な失敗回数。</summary>
    internal const int MaxFailedAttempts = 10;

    /// <summary>同時に保持する主体の上限。ランダムキー噴射でもプロセスを膨らませない。</summary>
    internal const int MaxTrackedSubjects = 4096;

    /// <summary>失敗を数える窓。</summary>
    internal static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    /// <summary>閾値到達後のロック時間。</summary>
    internal static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly TimeProvider _timeProvider;
    private readonly int _maxTrackedSubjects;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _mutateGate = new();

    /// <summary>本番用の既定上限で初期化する。</summary>
    /// <param name="timeProvider">現在時刻。テストで固定する。</param>
    internal InMemoryLoginFailureLockStore(TimeProvider timeProvider)
        : this(timeProvider, MaxTrackedSubjects)
    {
    }

    /// <summary>保持上限を指定して初期化する。</summary>
    /// <param name="timeProvider">現在時刻。テストで固定する。</param>
    /// <param name="maxTrackedSubjects">同時に保持する主体の上限。1 以上。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTrackedSubjects"/> が 1 未満。</exception>
    internal InMemoryLoginFailureLockStore(TimeProvider timeProvider, int maxTrackedSubjects)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTrackedSubjects, 1);
        _timeProvider = timeProvider;
        _maxTrackedSubjects = maxTrackedSubjects;
    }

    /// <summary>保持中の主体数。テスト用。</summary>
    internal int TrackedCount => _entries.Count;

    /// <inheritdoc />
    public bool IsLocked(string tenantKey, string username)
    {
        if (!_entries.TryGetValue(CreateKey(tenantKey, username), out var entry))
        {
            return false;
        }

        lock (entry)
        {
            var now = _timeProvider.GetUtcNow();
            return entry.LockedUntil is { } lockedUntil && lockedUntil > now;
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string tenantKey, string username)
    {
        var key = CreateKey(tenantKey, username);
        lock (_mutateGate)
        {
            if (!_entries.ContainsKey(key) && _entries.Count >= _maxTrackedSubjects)
            {
                EvictOneUnlocked();
                if (!_entries.ContainsKey(key) && _entries.Count >= _maxTrackedSubjects)
                {
                    return;
                }
            }

            var entry = _entries.GetOrAdd(key, static _ => new Entry());
            lock (entry)
            {
                var now = _timeProvider.GetUtcNow();
                if (entry.LockedUntil is { } lockedUntil && lockedUntil > now)
                {
                    return;
                }

                var windowStart = now - FailureWindow;
                entry.Failures.RemoveAll(failure => failure <= windowStart);
                entry.Failures.Add(now);
                if (entry.Failures.Count >= MaxFailedAttempts)
                {
                    entry.LockedUntil = now + LockDuration;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Reset(string tenantKey, string username)
    {
        lock (_mutateGate)
        {
            _entries.TryRemove(CreateKey(tenantKey, username), out _);
        }
    }

    private void EvictOneUnlocked()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _entries)
        {
            lock (pair.Value)
            {
                if (pair.Value.LockedUntil is { } lockedUntil && lockedUntil > now)
                {
                    continue;
                }
            }

            _entries.TryRemove(pair.Key, out _);
            return;
        }
    }

    private static string CreateKey(string tenantKey, string username) =>
        string.Concat(tenantKey, "\n", username);

    private sealed class Entry
    {
        public List<DateTimeOffset> Failures { get; } = [];

        public DateTimeOffset? LockedUntil { get; set; }
    }
}
