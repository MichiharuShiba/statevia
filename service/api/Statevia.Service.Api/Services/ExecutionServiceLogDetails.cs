namespace Statevia.Service.Api.Services;

/// <summary>
/// Serializable 永続化リトライログ用の構造化プロパティ。
/// </summary>
internal sealed class SerializablePersistRetryDetails
{
    public required string TraceId { get; init; }

    public Guid ExecutionId { get; init; }

    public Guid TenantId { get; init; }

    public Guid ClientEventId { get; init; }

    public int Attempt { get; init; }

    public int MaxAttempts { get; init; }

    public int DelayMs { get; init; }

    public required string FailureMessage { get; init; }
}

/// <summary>
/// イベント配送 dedup 決定ログ用の構造化プロパティ。
/// </summary>
internal sealed class EventDeliveryDecisionDetails
{
    public required string TraceId { get; init; }

    public Guid ExecutionId { get; init; }

    public Guid TenantId { get; init; }

    public Guid ClientEventId { get; init; }

    public required string Decision { get; init; }

    public int Attempt { get; init; }

    public long ElapsedMs { get; init; }

    public required string ErrorCode { get; init; }
}
