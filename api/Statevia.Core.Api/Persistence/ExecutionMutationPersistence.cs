using System.Data;
using System.Data.Common;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Persistence;

/// <summary>
/// <see cref="IExecutionMutationPersistence"/> の実装。
/// </summary>
internal sealed class ExecutionMutationPersistence : IExecutionMutationPersistence
{
    private readonly record struct SerializablePersistenceRetryProgress(
        int Attempt,
        int MaxAttempts,
        int TotalBackoffMs);

    private readonly ICoreUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IEventDeliveryDedupRepository _eventDeliveryDedup;
    private readonly IOptions<EventDeliveryRetryOptions> _eventDeliveryRetryOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExecutionMutationPersistence> _logger;

    /// <summary>
    /// 新しいインスタンスを初期化する。
    /// </summary>
    public ExecutionMutationPersistence(
        ICoreUnitOfWorkFactory unitOfWorkFactory,
        IEventDeliveryDedupRepository eventDeliveryDedup,
        IOptions<EventDeliveryRetryOptions> eventDeliveryRetryOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExecutionMutationPersistence> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _eventDeliveryDedup = eventDeliveryDedup;
        _eventDeliveryRetryOptions = eventDeliveryRetryOptions;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteSerializableWithRetryAsync(
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        Func<ICoreUnitOfWork, CancellationToken, Task> applyAsync,
        CancellationToken cancellationToken = default)
    {
        var retryOptions = _eventDeliveryRetryOptions.Value;
        var maxAttempts = Math.Max(1, retryOptions.SerializablePersistenceMaxAttempts);
        var totalBackoffMs = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
            await uow.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

            async Task PersistFailureAsync(Exception failure)
            {
                await TryRollbackSerializableTransactionAsync(uow, cancellationToken).ConfigureAwait(false);
                totalBackoffMs = await ApplySerializablePersistenceFailureAsync(
                    failure,
                    tenantId,
                    executionId,
                    clientEventId,
                    new SerializablePersistenceRetryProgress(attempt, maxAttempts, totalBackoffMs),
                    retryOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await applyAsync(uow, cancellationToken).ConfigureAwait(false);
                await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (EventDeliveryRetryPolicy.IsNonRetryableTimeoutOrCancellation(ex))
            {
                await TryRollbackSerializableTransactionAsync(uow, cancellationToken).ConfigureAwait(false);
                await TryMarkEventDeliveryFailedAsync(tenantId, executionId, clientEventId, cancellationToken)
                    .ConfigureAwait(false);
                throw;
            }
            catch (DbUpdateException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                await PersistFailureAsync(ex).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Serializable persistence retry loop ended unexpectedly.");
    }

    private static async Task TryRollbackSerializableTransactionAsync(
        ICoreUnitOfWork uow,
        CancellationToken cancellationToken)
    {
        try
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // 破損済みトランザクションのロールバックで起きうる競合は無視する。
        }
        catch (InvalidOperationException)
        {
            // 接続切断後など、既に破棄されたトランザクションのロールバック失敗は無視する。
        }
    }

    private async Task<int> ApplySerializablePersistenceFailureAsync(
        Exception ex,
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        SerializablePersistenceRetryProgress retryProgress,
        EventDeliveryRetryOptions retryOptions,
        CancellationToken cancellationToken)
    {
        var conflict = EventDeliveryRetryPolicy.IsPostgresSerializableOrDeadlockConflict(ex);
        if (conflict && retryProgress.Attempt < retryProgress.MaxAttempts)
        {
            var failureIndex = retryProgress.Attempt - 1;
            var delayMs = EventDeliveryRetryPolicy.ComputeBackoffDelayMs(failureIndex, retryOptions, Random.Shared);
            if (retryOptions.MaxTotalBackoffMs > 0)
            {
                var remainingBudgetMs = retryOptions.MaxTotalBackoffMs - retryProgress.TotalBackoffMs;
                if (remainingBudgetMs <= 0)
                {
                    LogSerializablePersistRetry(
                        tenantId,
                        executionId,
                        clientEventId,
                        retryProgress.Attempt,
                        retryProgress.MaxAttempts,
                        delayMs: 0,
                        ex.Message);
                    await TryMarkEventDeliveryFailedAsync(tenantId, executionId, clientEventId, cancellationToken)
                        .ConfigureAwait(false);
                    throw new InvalidOperationException(
                        "Serializable persistence retry stopped: total backoff budget exhausted.",
                        ex);
                }

                delayMs = Math.Min(delayMs, remainingBudgetMs);
            }

            var newTotalBackoffMs = retryProgress.TotalBackoffMs + delayMs;
            LogSerializablePersistRetry(
                tenantId,
                executionId,
                clientEventId,
                retryProgress.Attempt,
                retryProgress.MaxAttempts,
                delayMs,
                ex.Message);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            return newTotalBackoffMs;
        }

        if (conflict)
        {
            LogSerializablePersistRetry(
                tenantId,
                executionId,
                clientEventId,
                retryProgress.Attempt,
                retryProgress.MaxAttempts,
                delayMs: 0,
                ex.Message);
        }

        await TryMarkEventDeliveryFailedAsync(tenantId, executionId, clientEventId, cancellationToken)
            .ConfigureAwait(false);
        ExceptionDispatchInfo.Capture(ex).Throw();
        return 0;
    }

    private void LogSerializablePersistRetry(
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        int attempt,
        int maxAttempts,
        int delayMs,
        string failureMessage)
    {
        _logger.SerializablePersistRetry(new SerializablePersistRetryDetails
        {
            TraceId = GetTraceIdOrEmpty(),
            ExecutionId = executionId,
            TenantId = tenantId,
            ClientEventId = clientEventId,
            Attempt = attempt,
            MaxAttempts = maxAttempts,
            DelayMs = delayMs,
            FailureMessage = failureMessage,
        });
    }

    private string GetTraceIdOrEmpty()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(RequestLogContext.TraceIdItemKey, out var traceId) == true
            && traceId is string traceIdString
            && !string.IsNullOrEmpty(traceIdString))
        {
            return traceIdString;
        }

        return string.Empty;
    }

    private async Task TryMarkEventDeliveryFailedAsync(
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
            var nowUtc = DateTime.UtcNow;
            await _eventDeliveryDedup.TryUpdateStatusAsync(
                uow,
                tenantId,
                executionId,
                clientEventId,
                new EventDeliveryDedupStatusUpdate(
                    EventDeliveryDedupStatuses.Failed,
                    nowUtc,
                    AppliedAt: null,
                    ErrorCode: "persist_failed"),
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types — 補助更新の失敗は本例外に影響させない
        catch
        {
            // 意図的に飲み込む
        }
#pragma warning restore CA1031
    }
}
