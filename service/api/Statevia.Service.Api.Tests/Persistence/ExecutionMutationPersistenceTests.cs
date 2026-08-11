using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Statevia.Core.Application.Configuration;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence;

/// <summary>ExecutionMutationPersistence の単体テスト。</summary>
public sealed class ExecutionMutationPersistenceTests
{
    private static readonly IOptions<EventDeliveryRetryOptions> RetryOptions = Options.Create(
        new EventDeliveryRetryOptions
        {
            MaxAttempts = 3,
            BaseDelayMs = 0,
            MaxDelayMs = 1,
            Jitter = false,
            MaxTotalBackoffMs = 10_000,
            SerializablePersistenceMaxAttempts = 3
        });

    /// <summary>成功時は apply → SaveChanges → Commit で完了する。</summary>
    [Fact]
    public async Task ExecuteSerializableWithRetryAsync_WhenApplySucceeds_CommitsOnce()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var dedup = new RecordingEventDeliveryDedup();
        var sut = CreateSut(sqlite, dedup);
        var applyCalls = 0;

        // Act
        await sut.ExecuteSerializableWithRetryAsync(
            TestTenantIds.T1TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            (_, _) =>
            {
                applyCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(1, applyCalls);
        Assert.Equal(0, dedup.FailedStatusUpdates);
    }

    /// <summary>再試行可能な Serializable 競合は maxAttempts まで再実行する。</summary>
    [Fact]
    public async Task ExecuteSerializableWithRetryAsync_WhenSerializableConflictRetriesExhausted_MarksFailedAndThrows()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var dedup = new RecordingEventDeliveryDedup();
        var sut = CreateSut(sqlite, dedup);
        var applyCalls = 0;

        // Act
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            sut.ExecuteSerializableWithRetryAsync(
                TestTenantIds.T1TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                (_, _) =>
                {
                    applyCalls++;
                    var pg = new PostgresException(
                        messageText: "could not serialize access",
                        severity: "ERROR",
                        invariantSeverity: "ERROR",
                        sqlState: "40001");
                    throw new DbUpdateException("serialize conflict", pg);
                },
                CancellationToken.None));

        // Assert
        Assert.Equal(3, applyCalls);
        Assert.IsType<PostgresException>(ex.InnerException);
        Assert.True(dedup.FailedStatusUpdates >= 1);
    }

    /// <summary>タイムアウト系は再試行せず Failed を付けて再throw する。</summary>
    [Fact]
    public async Task ExecuteSerializableWithRetryAsync_WhenOperationCanceled_MarksFailedAndRethrows()
    {
        // Arrange
        using var sqlite = new SqliteTestDatabase();
        var dedup = new RecordingEventDeliveryDedup();
        var sut = CreateSut(sqlite, dedup);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.ExecuteSerializableWithRetryAsync(
                TestTenantIds.T1TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                (_, _) => throw new OperationCanceledException("cancelled in apply"),
                CancellationToken.None));

        // Assert
        Assert.True(dedup.FailedStatusUpdates >= 1);
    }

    private static ExecutionMutationPersistence CreateSut(
        SqliteTestDatabase sqlite,
        IEventDeliveryDedupRepository dedup)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[RequestLogContext.TraceIdItemKey] = "trace-mutation";
        return new ExecutionMutationPersistence(
            new TestCoreUnitOfWorkFactory(sqlite.Factory),
            dedup,
            RetryOptions,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<ExecutionMutationPersistence>.Instance);
    }

    private sealed class RecordingEventDeliveryDedup : IEventDeliveryDedupRepository
    {
        public int FailedStatusUpdates { get; private set; }

        public Task AddReceivedAsync(
            ICoreUnitOfWork uow,
            EventDeliveryDedupRow row,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<EventDeliveryDedupRow?> FindAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<EventDeliveryDedupRow?>(null);

        public Task<bool> TryUpdateStatusAsync(
            ICoreUnitOfWork uow,
            Guid tenantId,
            Guid executionId,
            Guid clientEventId,
            EventDeliveryDedupStatusUpdate update,
            CancellationToken cancellationToken)
        {
            _ = uow;
            if (update.Status == EventDeliveryDedupStatuses.Failed)
                FailedStatusUpdates++;
            return Task.FromResult(true);
        }
    }
}
