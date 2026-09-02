using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="InMemoryLoginFailureLockStore"/> の窓とロック時間。</summary>
public sealed class InMemoryLoginFailureLockStoreTests
{
    /// <summary>閾値未満ではロックしない。</summary>
    [Fact]
    public void IsLocked_BelowThreshold_IsFalse()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time);

        // Act
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts - 1; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>閾値到達でロックし、解除後は再試行できる。</summary>
    [Fact]
    public void RecordFailure_ReachingThreshold_LocksUntilDurationElapses()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time);

        // Act
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.True(store.IsLocked("default", "admin"));

        time.UtcNowValue += InMemoryLoginFailureLockStore.LockDuration;
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>窓を外れた失敗は数えない。</summary>
    [Fact]
    public void RecordFailure_FailuresOutsideWindow_DoNotLock()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time);
        store.RecordFailure("default", "admin");

        // Act
        time.UtcNowValue += InMemoryLoginFailureLockStore.FailureWindow + TimeSpan.FromSeconds(1);
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts - 1; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>Reset でロックが消える。</summary>
    [Fact]
    public void Reset_ClearsLock()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time);
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Act
        store.Reset("default", "admin");

        // Assert
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>他ユーザーの失敗は共有しない。</summary>
    [Fact]
    public void RecordFailure_DoesNotShareCountAcrossUsers()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time);

        // Act
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.True(store.IsLocked("default", "admin"));
        Assert.False(store.IsLocked("default", "other"));
    }

    /// <summary>上限に達したら未ロック主体を追い出し、件数を超えない。</summary>
    [Fact]
    public void RecordFailure_WhenAtCapacity_EvictsUnlockedSubject()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time, maxTrackedSubjects: 2);
        store.RecordFailure("default", "one");
        store.RecordFailure("default", "two");

        // Act
        store.RecordFailure("default", "three");

        // Assert
        Assert.Equal(2, store.TrackedCount);
        for (var attempt = 1; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "three");
        }

        Assert.True(store.IsLocked("default", "three"));
        Assert.Equal(2, store.TrackedCount);
    }

    /// <summary>上限がすべてロック中なら新規を捨てる（fail-open）。</summary>
    [Fact]
    public void RecordFailure_WhenAllLockedAtCapacity_DoesNotAddSubject()
    {
        // Arrange
        var time = new MutableTimeProvider();
        var store = new InMemoryLoginFailureLockStore(time, maxTrackedSubjects: 1);
        for (var attempt = 0; attempt < InMemoryLoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "locked");
        }

        // Act
        store.RecordFailure("default", "other");

        // Assert
        Assert.Equal(1, store.TrackedCount);
        Assert.True(store.IsLocked("default", "locked"));
        Assert.False(store.IsLocked("default", "other"));
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
