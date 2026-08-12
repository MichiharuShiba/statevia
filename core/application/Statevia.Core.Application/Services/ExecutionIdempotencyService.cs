using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Configuration;
using Statevia.Core.Application.Infrastructure;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行コマンド / イベント配送の冪等判定と再送復元。
/// </summary>
/// <remarks>
/// <para><c>command_dedup</c> のヒット復元・衝突検出と、<c>event_delivery_dedup</c> の RECEIVED 先行挿入を担う。</para>
/// <para>dedup 行の APPLIED 更新や Save は呼び出し側のミューテーション tx に残す。</para>
/// </remarks>
/// <param name="dedupService">command dedup キー組み立て。</param>
/// <param name="commandDedup">command_dedup 永続化。</param>
/// <param name="eventDeliveryDedup">event_delivery_dedup 永続化。</param>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="eventDeliveryRetryOptions">イベント配送挿入の再試行設定。</param>
/// <param name="correlationIdAccessor">相関 ID。</param>
/// <param name="logger">構造化ログ。</param>
internal sealed class ExecutionIdempotencyService(
    ICommandDedupService dedupService,
    ICommandDedupRepository commandDedup,
    IEventDeliveryDedupRepository eventDeliveryDedup,
    ITenantContextAccessor tenantContext,
    ICoreTransactionExecutor executor,
    IOptions<EventDeliveryRetryOptions> eventDeliveryRetryOptions,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<ExecutionIdempotencyService> logger)
{
    /// <summary><c>event_delivery_decision</c> 構造化ログの <c>decision</c> プロパティ値。</summary>
    private static class EventDeliveryLogDecisions
    {
        internal const string Inserted = "inserted";
        internal const string DuplicateKey = "duplicate_key";
        internal const string AbortedTimeout = "aborted_timeout";
        internal const string Retry = "retry";
        internal const string BackoffBudgetExhausted = "backoff_budget_exhausted";
        internal const string Failed = "failed";
    }

    /// <summary>
    /// <c>event_delivery_decision</c> 構造化ログの <c>errorCode</c>、および dedup 行の既知 <c>error_code</c> 値。
    /// </summary>
    private static class EventDeliveryLogErrorCodes
    {
        internal const string None = "none";
        internal const string UniqueViolation = "unique_violation";
    }

    /// <summary>command dedup キーを組み立てる（キー未指定時は <see langword="null"/>）。</summary>
    /// <param name="tenantKey">テナントキー。</param>
    /// <param name="idempotencyKey">冪等キー。</param>
    /// <param name="method">HTTP メソッド相当。</param>
    /// <param name="path">パス。</param>
    /// <param name="requestHash">任意のリクエストハッシュ（Start 用）。</param>
    /// <returns>dedup キー。未指定時は null。</returns>
    public CommandDedupKey? CreateKey(
        string tenantKey,
        string? idempotencyKey,
        string method,
        string path,
        string? requestHash = null) =>
        dedupService.Create(tenantKey, idempotencyKey, method, path, requestHash);

    /// <summary>
    /// Start の command_dedup からキャッシュ応答を復元する。衝突ハッシュがあれば例外。
    /// </summary>
    /// <param name="dedupKey">command dedup キー。</param>
    /// <param name="requestHash">今回のリクエストハッシュ。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>キャッシュされた Start 応答。無ければ null。</returns>
    public async Task<ExecutionResponse?> TryGetIdempotentStartResponseAsync(
        CommandDedupKey? dedupKey,
        string requestHash,
        CancellationToken ct)
    {
        if (dedupKey is not { } key)
        {
            return null;
        }

        var tenantKey = tenantContext.GetRequiredTenantKey();
        var dedupCheckTime = DateTime.UtcNow;
        var existing = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => commandDedup.FindValidAsync(uow, key.DedupKey, dedupCheckTime, innerCt),
            ct).ConfigureAwait(false);
        if (existing is not null)
        {
            var cached = DeserializeCachedExecutionResponse(existing);
            if (cached is not null)
            {
                return cached;
            }
        }

        var conflicting = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => commandDedup.FindValidConflictingRequestHashAsync(
                uow,
                tenantKey,
                key.Endpoint,
                key.IdempotencyKey,
                requestHash,
                dedupCheckTime,
                innerCt),
            ct).ConfigureAwait(false);
        if (conflicting is not null)
        {
            throw new IdempotencyConflictException(
                "The same X-Idempotency-Key was used with a different request body.");
        }

        return null;
    }

    /// <summary>
    /// 有効な command_dedup 行が存在するかどうか（Cancel / Publish / Resume の早期 return 用）。
    /// </summary>
    /// <param name="dedupKey">command dedup キー。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>有効行があれば true。</returns>
    public async Task<bool> HasValidCommandDedupAsync(CommandDedupKey? dedupKey, CancellationToken ct)
    {
        if (dedupKey is not { } key)
            return false;

        var dedupCheckTime = DateTime.UtcNow;
        var existing = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => commandDedup.FindValidAsync(uow, key.DedupKey, dedupCheckTime, innerCt),
            ct).ConfigureAwait(false);
        return existing is not null;
    }

    /// <summary>
    /// <c>event_delivery_dedup</c> に RECEIVED を先行挿入する。一意制約違反時は既存行を読み、
    /// 既に APPLIED なら true（呼び出し側は即 return）。それ以外は false（処理継続）。
    /// DB 一時障害時は設定に従い段階的バックオフで再試行する（タイムアウト系は再試行しない）。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="clientEventId">クライアントイベント ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>既に APPLIED なら true。</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Critical Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "イベント配送 dedup の再試行・ログ分岐を一箇所に集約している。")]
    public async Task<bool> TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        var retryOptions = eventDeliveryRetryOptions.Value;
        var traceId = correlationIdAccessor.GetCorrelationId();
        var tenantId = tenantContext.GetRequiredTenantId();
        var acceptedAt = DateTime.UtcNow;
        var row = new EventDeliveryDedupRow
        {
            TenantId = tenantId,
            ExecutionId = executionId,
            ClientEventId = clientEventId,
            BatchId = null,
            Status = EventDeliveryDedupStatuses.Received,
            AcceptedAt = acceptedAt,
            AppliedAt = null,
            ErrorCode = null,
            UpdatedAt = acceptedAt
        };

        var totalBackoffMs = 0;

        for (var attempt = 1; attempt <= retryOptions.MaxAttempts; attempt++)
        {
            var attemptStopwatch = Stopwatch.StartNew();
            try
            {
                await executor.ExecuteReadCommittedAsync(
                    async (uow, innerCt) =>
                    {
                        await eventDeliveryDedup.AddReceivedAsync(uow, row, innerCt).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(new EventDeliveryDecisionDetails
                {
                    TraceId = traceId,
                    TenantId = tenantId,
                    ExecutionId = executionId,
                    ClientEventId = clientEventId,
                    Decision = EventDeliveryLogDecisions.Inserted,
                    Attempt = attempt,
                    ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                    ErrorCode = EventDeliveryLogErrorCodes.None,
                });
                return false;
            }
            catch (DbUpdateException dbUpdateException)
                when (EventDeliveryRetryPolicy.IsUniqueConstraintViolation(dbUpdateException))
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(new EventDeliveryDecisionDetails
                {
                    TraceId = traceId,
                    TenantId = tenantId,
                    ExecutionId = executionId,
                    ClientEventId = clientEventId,
                    Decision = EventDeliveryLogDecisions.DuplicateKey,
                    Attempt = attempt,
                    ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                    ErrorCode = EventDeliveryLogErrorCodes.UniqueViolation,
                });

                var existing = await executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => eventDeliveryDedup.FindAsync(uow, tenantId, executionId, clientEventId, innerCt),
                    cancellationToken).ConfigureAwait(false);
                if (existing is { Status: EventDeliveryDedupStatuses.Applied })
                    return true;

                if (existing is null)
                    throw;

                return false;
            }
            catch (Exception exception)
                when (EventDeliveryRetryPolicy.IsNonRetryableTimeoutOrCancellation(exception))
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = executionId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.AbortedTimeout,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
                    exception);
                throw;
            }
            catch (Exception exception) when (
                EventDeliveryRetryPolicy.IsTransientInfrastructureFailure(exception)
                && attempt < retryOptions.MaxAttempts)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = executionId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.Retry,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
                    exception);

                var failureIndex = attempt - 1;
                var delayMs = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(
                    failureIndex,
                    retryOptions,
                    Random.Shared);

                if (retryOptions.MaxTotalBackoffMs > 0)
                {
                    var remainingBudgetMs = retryOptions.MaxTotalBackoffMs - totalBackoffMs;
                    if (remainingBudgetMs <= 0)
                    {
                        attemptStopwatch.Stop();
                        LogEventDeliveryDecision(
                            new EventDeliveryDecisionDetails
                            {
                                TraceId = traceId,
                                TenantId = tenantId,
                                ExecutionId = executionId,
                                ClientEventId = clientEventId,
                                Decision = EventDeliveryLogDecisions.BackoffBudgetExhausted,
                                Attempt = attempt,
                                ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                                ErrorCode = EventDeliveryLogDecisions.BackoffBudgetExhausted,
                            },
                            exception);
                        throw new InvalidOperationException(
                            "Event delivery insert retry stopped: total backoff budget exhausted.",
                            exception);
                    }

                    delayMs = Math.Min(delayMs, remainingBudgetMs);
                }

                totalBackoffMs += delayMs;
                if (delayMs > 0)
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                attemptStopwatch.Stop();
                LogEventDeliveryDecision(
                    new EventDeliveryDecisionDetails
                    {
                        TraceId = traceId,
                        TenantId = tenantId,
                        ExecutionId = executionId,
                        ClientEventId = clientEventId,
                        Decision = EventDeliveryLogDecisions.Failed,
                        Attempt = attempt,
                        ElapsedMs = attemptStopwatch.ElapsedMilliseconds,
                        ErrorCode = MapErrorCode(exception),
                    },
                    exception);
                throw;
            }
        }

        throw new InvalidOperationException("Event delivery insert retry loop ended unexpectedly.");
    }

    private static ExecutionResponse? DeserializeCachedExecutionResponse(CommandDedupRow existing)
    {
        if (string.IsNullOrEmpty(existing.ResponseBody))
            return null;
        return JsonDeserialize.TryDeserialize<ExecutionResponse>(
            existing.ResponseBody,
            JsonSerializerProfiles.CaseInsensitive,
            out var deserialized)
            ? deserialized
            : null;
    }

    private void LogEventDeliveryDecision(EventDeliveryDecisionDetails details, Exception? exception = null)
    {
        switch (details.Decision)
        {
            case EventDeliveryLogDecisions.Failed:
            case EventDeliveryLogDecisions.BackoffBudgetExhausted:
                logger.EventDeliveryDecisionError(exception!, details);
                break;
            case EventDeliveryLogDecisions.Retry:
            case EventDeliveryLogDecisions.AbortedTimeout:
                logger.EventDeliveryDecisionWarning(exception!, details);
                break;
            default:
                logger.EventDeliveryDecisionInformation(details);
                break;
        }
    }

    /// <summary>ログ用のエラー分類コード（例外種別・SQLSTATE 等の短い識別子）。</summary>
    private static string MapErrorCode(Exception exception) =>
        exception switch
        {
            TaskCanceledException => nameof(TaskCanceledException),
            OperationCanceledException => nameof(OperationCanceledException),
            TimeoutException => nameof(TimeoutException),
            DbUpdateException dbUpdateException =>
                dbUpdateException.InnerException?.GetType().Name ?? nameof(DbUpdateException),
            _ => exception.GetType().Name
        };
}
