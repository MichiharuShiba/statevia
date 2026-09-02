using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="LoginFailureLockStore"/> の窓・ロック・件数上限なし。</summary>
public sealed class LoginFailureLockStoreTests
{
    /// <summary>閾値未満ではロックしない。</summary>
    [Fact]
    public void IsLocked_BelowThreshold_IsFalse()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);

        // Act
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts - 1; attempt++)
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
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);

        // Act
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.True(store.IsLocked("default", "admin"));

        time.UtcNowValue += LoginFailureLockStore.LockDuration;
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>窓を外れた失敗は数えない。</summary>
    [Fact]
    public void RecordFailure_FailuresOutsideWindow_DoNotLock()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);
        store.RecordFailure("default", "admin");

        // Act
        time.UtcNowValue += LoginFailureLockStore.FailureWindow + TimeSpan.FromSeconds(1);
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts - 1; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.False(store.IsLocked("default", "admin"));
    }

    /// <summary>書き込み時に窓外 attempts を DELETE する。</summary>
    [Fact]
    public void RecordFailure_DeletesAttemptsOutsideWindow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);
        store.RecordFailure("default", "admin");

        // Act
        time.UtcNowValue += LoginFailureLockStore.FailureWindow + TimeSpan.FromSeconds(1);
        store.RecordFailure("default", "admin");

        // Assert
        using var db = database.Factory.CreateDbContext();
        Assert.Equal(1, db.LoginFailureAttempts.Count(row => row.Username == "admin"));
    }

    /// <summary>Reset でロックと失敗行が消える。</summary>
    [Fact]
    public void Reset_ClearsLockAndAttempts()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Act
        store.Reset("default", "admin");

        // Assert
        Assert.False(store.IsLocked("default", "admin"));
        using var db = database.Factory.CreateDbContext();
        Assert.Equal(0, db.LoginFailureLocks.Count(row => row.Username == "admin"));
        Assert.Equal(0, db.LoginFailureAttempts.Count(row => row.Username == "admin"));
    }

    /// <summary>他ユーザーの失敗は共有しない。</summary>
    [Fact]
    public void RecordFailure_DoesNotShareCountAcrossUsers()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);

        // Act
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "admin");
        }

        // Assert
        Assert.True(store.IsLocked("default", "admin"));
        Assert.False(store.IsLocked("default", "other"));
    }

    /// <summary>4096 超の実在キーでも回数が消えない。</summary>
    [Fact]
    public void RecordFailure_MoreThanFormerCapacity_StillLocksTarget()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider();
        var store = CreateStore(database, time);
        using (var seed = database.Factory.CreateDbContext())
        {
            var extras = Enumerable.Range(0, 4096)
                .Select(index => new LoginFailureLockRow
                {
                    TenantKey = "default",
                    Username = $"u{index:D4}"
                })
                .ToList();
            seed.LoginFailureLocks.AddRange(extras);
            seed.SaveChanges();
        }

        // Act
        for (var attempt = 0; attempt < LoginFailureLockStore.MaxFailedAttempts; attempt++)
        {
            store.RecordFailure("default", "target");
        }

        // Assert
        Assert.True(store.IsLocked("default", "target"));
        using var db = database.Factory.CreateDbContext();
        Assert.Equal(4097, db.LoginFailureLocks.Count());
        Assert.Equal(4096, db.LoginFailureLocks.Count(row => row.Username != "target"));
    }

    /// <summary>窓外 attempts と、ロック切れかつ窓内失敗なしの locks を DELETE する。</summary>
    [Fact]
    public void PurgeExpired_DeletesStaleAttemptsAndUnlockedRows()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var time = new MutableTimeProvider { UtcNowValue = DateTimeOffset.UnixEpoch.AddHours(1) };
        var store = CreateStore(database, time);
        var now = time.UtcNowValue.UtcDateTime;
        using (var seed = database.Factory.CreateDbContext())
        {
            seed.LoginFailureLocks.AddRange(
            [
                new LoginFailureLockRow
                {
                    TenantKey = "default",
                    Username = "stale",
                    LockedUntil = now.AddMinutes(-1)
                },
                new LoginFailureLockRow
                {
                    TenantKey = "default",
                    Username = "active",
                    LockedUntil = now.AddMinutes(5)
                },
                new LoginFailureLockRow
                {
                    TenantKey = "default",
                    Username = "recent"
                }
            ]);
            seed.LoginFailureAttempts.AddRange(
            [
                new LoginFailureAttemptRow
                {
                    AttemptId = Guid.Parse("01800000-0000-7000-8000-000000000010"),
                    TenantKey = "default",
                    Username = "stale",
                    FailedAt = now - LoginFailureLockStore.FailureWindow - TimeSpan.FromSeconds(1)
                },
                new LoginFailureAttemptRow
                {
                    AttemptId = Guid.Parse("01800000-0000-7000-8000-000000000011"),
                    TenantKey = "default",
                    Username = "active",
                    FailedAt = now - TimeSpan.FromMinutes(1)
                },
                new LoginFailureAttemptRow
                {
                    AttemptId = Guid.Parse("01800000-0000-7000-8000-000000000012"),
                    TenantKey = "default",
                    Username = "recent",
                    FailedAt = now - TimeSpan.FromMinutes(1)
                }
            ]);
            seed.SaveChanges();
        }

        // Act
        store.PurgeExpired();

        // Assert
        using var db = database.Factory.CreateDbContext();
        Assert.False(db.LoginFailureLocks.Any(row => row.Username == "stale"));
        Assert.False(db.LoginFailureAttempts.Any(row => row.Username == "stale"));
        Assert.True(db.LoginFailureLocks.Any(row => row.Username == "active"));
        Assert.True(db.LoginFailureLocks.Any(row => row.Username == "recent"));
        Assert.Equal(2, db.LoginFailureAttempts.Count());
    }

    private static LoginFailureLockStore CreateStore(SqliteTestDatabase database, TimeProvider timeProvider) =>
        new(
            database.Factory,
            timeProvider,
            new DefaultIdGenerator(),
            NullLogger<LoginFailureLockStore>.Instance);

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
