using Microsoft.Extensions.Logging;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionServiceLogMessages"/> とログ詳細 DTO のテスト。</summary>
public sealed class ExecutionServiceLogMessagesTests
{
    /// <summary>Serializable 永続化リトライログを出力する。</summary>
    [Fact]
    public void SerializablePersistRetry_EmitsStructuredLog()
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<ExecutionService>();
        var details = new SerializablePersistRetryDetails
        {
            TraceId = "trace-1",
            ExecutionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TenantId = TestTenantIds.DefaultTenantId,
            ClientEventId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Attempt = 1,
            MaxAttempts = 3,
            DelayMs = 50,
            FailureMessage = "transient"
        };

        // Act
        logger.SerializablePersistRetry(details);

        // Assert
        Assert.Contains(collector.Entries, e => e.Contains("serializable_persist_retry", StringComparison.Ordinal));
    }

    /// <summary>イベント配送 dedup の各レベルログを出力する。</summary>
    [Theory]
    [InlineData("information")]
    [InlineData("warning")]
    [InlineData("error")]
    public void EventDeliveryDecision_LogsAtEachLevel(string level)
    {
        // Arrange
        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<ExecutionService>();
        var details = new EventDeliveryDecisionDetails
        {
            TraceId = "trace-2",
            ExecutionId = Guid.NewGuid(),
            TenantId = TestTenantIds.T1TenantId,
            ClientEventId = Guid.NewGuid(),
            Decision = level,
            Attempt = 2,
            ElapsedMs = 10,
            ErrorCode = "X"
        };
        var ex = new InvalidOperationException("dedup");

        // Act
        switch (level)
        {
            case "information":
                logger.EventDeliveryDecisionInformation(details);
                break;
            case "warning":
                logger.EventDeliveryDecisionWarning(ex, details);
                break;
            case "error":
                logger.EventDeliveryDecisionError(ex, details);
                break;
        }

        // Assert
        Assert.Contains(collector.Entries, e => e.Contains("event_delivery_decision", StringComparison.Ordinal));
    }

    private sealed class LogCollector : ILoggerProvider, IDisposable
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CollLogger : ILogger
        {
            private readonly List<string> _entries;

            public CollLogger(List<string> entries) => _entries = entries;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _entries.Add(formatter(state, exception));
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
