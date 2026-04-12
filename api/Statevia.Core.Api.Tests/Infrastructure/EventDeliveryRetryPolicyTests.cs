using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary><see cref="EventDeliveryRetryPolicy"/> のバックオフ計算と再試行可否の単体テスト。</summary>
public sealed class EventDeliveryRetryPolicyTests
{
    /// <summary>ジッタ無効時は指数で遅延が増える。</summary>
    [Fact]
    public void ComputeBackoffDelayMs_WithoutJitter_DoublesEachStep()
    {
        // Arrange
        var options = new EventDeliveryRetryOptions
        {
            BaseDelayMs = 10,
            MaxDelayMs = 10_000,
            Jitter = false
        };
        var random = new Random(1);

        // Act
        var first = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(0, options, random);
        var second = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(1, options, random);

        // Assert
        Assert.Equal(10, first);
        Assert.Equal(20, second);
    }

    /// <summary>IOException はインフラの一時障害とみなす。</summary>
    [Fact]
    public void IsTransientInfrastructureFailure_ReturnsTrue_ForIoException()
    {
        // Act & Assert
        Assert.True(EventDeliveryRetryPolicy.IsTransientInfrastructureFailure(new IOException("net")));
    }

    /// <summary>キャンセル系は再試行しない。</summary>
    [Fact]
    public void IsNonRetryableTimeoutOrCancellation_ReturnsTrue_ForTaskCanceled()
    {
        // Act & Assert
        Assert.True(EventDeliveryRetryPolicy.IsNonRetryableTimeoutOrCancellation(new TaskCanceledException()));
    }

    /// <summary>PostgreSQL の 23505 を一意制約違反とみなす。</summary>
    [Fact]
    public void IsUniqueConstraintViolation_ReturnsTrue_ForPostgres23505()
    {
        // Arrange
        var inner = new PostgresException(
            messageText: "duplicate key",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "23505");
        var dbUpdate = new DbUpdateException("dup", inner);

        // Act & Assert
        Assert.True(EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdate));
    }

    /// <summary>PostgreSQL の 23505 以外は一意制約違反とみなさない。</summary>
    [Fact]
    public void IsUniqueConstraintViolation_ReturnsFalse_ForPostgresNonUniqueState()
    {
        // Arrange
        var inner = new PostgresException(
            messageText: "deadlock",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "40P01");
        var dbUpdate = new DbUpdateException("x", inner);

        // Act & Assert
        Assert.False(EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdate));
    }

    /// <summary>SQLite の制約エラー（19）を一意制約系の失敗として扱う。</summary>
    [Fact]
    public void IsUniqueConstraintViolation_ReturnsTrue_ForSqliteConstraint()
    {
        // Arrange
        var inner = new SqliteException("UNIQUE constraint failed", 19);
        var dbUpdate = new DbUpdateException("dup", inner);

        // Act & Assert
        Assert.True(EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdate));
    }
}
