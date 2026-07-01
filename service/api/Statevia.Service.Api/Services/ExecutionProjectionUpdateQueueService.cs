using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Services;

/// <summary>
/// ノード完了通知を受け、実行投影更新を逐次処理するキューサービス。
/// </summary>
internal sealed class ExecutionProjectionUpdateQueueService : BackgroundService, IExecutionProjectionUpdateQueue
{
    /// <summary>
    /// 再試行上限に達して退避した実行インスタンス情報。
    /// </summary>
    private sealed class DeadLetterEntry
    {
        internal required Guid ExecutionId { get; init; }
        internal required DateTime DeadLetteredAtUtc { get; init; }
        internal required int RetryCount { get; init; }
        internal required string ErrorType { get; init; }
        internal required string ErrorMessage { get; init; }
    }

    /// <summary>
    /// execution 単位の局所状態。
    /// </summary>
    private sealed class ExecutionQueueState
    {
        // 同一 execution の状態遷移はこの lock で直列化する。
        internal object Gate { get; } = new();
        // キュー投入済み（または再投入予約あり）を示すフラグ。
        internal bool IsQueued { get; set; }
        // 現在ワーカーが当該 execution を処理中かどうか。
        internal bool IsProcessing { get; set; }
        // デバウンス判定用の最終 enqueue 時刻。
        internal DateTime LastEnqueuedAtUtc { get; set; } = DateTime.UtcNow;
        // 連続失敗回数（成功で 0 に戻す）。
        internal int ConsecutiveFailureCount { get; set; }
        // dead-letter 退避済みかどうか。
        internal bool IsDeadLettered { get; set; }
        // drain 待ちの同期に使う。idle になったら完了させる。
        internal TaskCompletionSource<bool> IdleSignal { get; set; } = CreateCompletedSignal();

        /// <summary>drain 待機用の未完了シグナルを生成する。</summary>
        internal static TaskCompletionSource<bool> CreatePendingSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>初期状態（idle）を表す完了済みシグナルを生成する。</summary>
        internal static TaskCompletionSource<bool> CreateCompletedSignal()
        {
            var taskCompletionSource = CreatePendingSignal();
            _ = taskCompletionSource.TrySetResult(true);
            return taskCompletionSource;
        }
    }

    private readonly Channel<Guid> _globalQueue;
    private readonly ConcurrentDictionary<Guid, ExecutionQueueState> _states = new();
    private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _deadLetters = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExecutionEngine _executionEngine;
    private readonly ILogger<ExecutionProjectionUpdateQueueService> _logger;
    private readonly int _debounceMs;
    private readonly int _maxRetryAttempts;
    private readonly int _retryBaseDelayMs;
    private readonly int _retryMaxDelayMs;

    public ExecutionProjectionUpdateQueueService(
        IServiceScopeFactory scopeFactory,
        IExecutionEngine executionEngine,
        IOptions<ExecutionProjectionQueueOptions> options,
        ILogger<ExecutionProjectionUpdateQueueService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queueOptions = options.Value;

        if (queueOptions.MaxGlobalQueueSize < 1)
            throw new ArgumentException("ExecutionProjectionQueue:MaxGlobalQueueSize must be >= 1");
        if (queueOptions.ProjectionFlushDebounceMs is < 0 or > 250)
            throw new ArgumentException("ExecutionProjectionQueue:ProjectionFlushDebounceMs must be between 0 and 250");
        if (queueOptions.MaxRetryAttempts < 1)
            throw new ArgumentException("ExecutionProjectionQueue:MaxRetryAttempts must be >= 1");
        if (queueOptions.RetryBaseDelayMs < 0)
            throw new ArgumentException("ExecutionProjectionQueue:RetryBaseDelayMs must be >= 0");
        if (queueOptions.RetryMaxDelayMs < queueOptions.RetryBaseDelayMs)
            throw new ArgumentException("ExecutionProjectionQueue:RetryMaxDelayMs must be >= RetryBaseDelayMs");

        _scopeFactory = scopeFactory;
        _executionEngine = executionEngine;
        _logger = logger;
        _debounceMs = queueOptions.ProjectionFlushDebounceMs;
        _maxRetryAttempts = queueOptions.MaxRetryAttempts;
        _retryBaseDelayMs = queueOptions.RetryBaseDelayMs;
        _retryMaxDelayMs = queueOptions.RetryMaxDelayMs;

        // global queue は有界。満杯時は WriteAsync が待機し、ドロップしない。
        _globalQueue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(queueOptions.MaxGlobalQueueSize)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(Guid executionId, CancellationToken ct)
    {
        var state = _states.GetOrAdd(executionId, _ => new ExecutionQueueState());
        var shouldWrite = false;

        lock (state.Gate)
        {
            if (state.IsDeadLettered)
            {
                // dead-letter 済みの execution は自動再開しない（手動オペレーション対象）。
                _logger.SkipEnqueueDeadLettered(executionId);
                return;
            }

            state.LastEnqueuedAtUtc = DateTime.UtcNow;
            if (!state.IsQueued && !state.IsProcessing)
            {
                // idle -> active 遷移。drain 待ちが同期できるよう pending signal を作り直す。
                if (state.IdleSignal.Task.IsCompleted)
                    state.IdleSignal = ExecutionQueueState.CreatePendingSignal();
                state.IsQueued = true;
                shouldWrite = true;
            }
            else
            {
                // 既に queue/processing 中なら、execution 単位 1 スロットに併合する。
                state.IsQueued = true;
            }
        }

        if (shouldWrite)
            await _globalQueue.Writer.WriteAsync(executionId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DrainAsync(Guid executionId, CancellationToken ct)
    {
        if (!_states.TryGetValue(executionId, out var state))
            return;

        // 「queued でも processing でもない」状態になるまで待機する。
        while (true)
        {
            Task waitTask;
            lock (state.Gate)
            {
                if (!state.IsQueued && !state.IsProcessing)
                    return;
                waitTask = state.IdleSignal.Task;
            }

            await waitTask.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Engine からの通知は executionId(string) で届くので Guid へ変換して queue へ流す。
        _executionEngine.SetNodeCompletedHandler(executionId =>
        {
            if (!Guid.TryParse(executionId, out var parsedExecutionId))
            {
                return Task.CompletedTask;
            }

            return EnqueueAsync(parsedExecutionId, stoppingToken);
        });

        return RunWorkerLoopAsync(stoppingToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _executionEngine.SetNodeCompletedHandler(null);
        await DrainPendingExecutionsOnShutdownAsync(cancellationToken).ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 正常停止時に既知 execution の pending / processing を可能な限りドレインする。
    /// </summary>
    private async Task DrainPendingExecutionsOnShutdownAsync(CancellationToken cancellationToken)
    {
        var executionIds = _states.Keys.ToArray();
        if (executionIds.Length == 0)
            return;

        var drainTasks = executionIds.Select(executionId => DrainAsync(executionId, cancellationToken));
        try
        {
            await Task.WhenAll(drainTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var remainingCount = CountActiveExecutions();
            _logger.DrainShutdownTimeout(remainingCount);
            throw;
        }
    }

    /// <summary>
    /// 未ドレイン（queued / processing）状態の execution 件数を数える。
    /// </summary>
    private int CountActiveExecutions()
    {
        var count = 0;
        foreach (var state in _states.Values)
        {
            lock (state.Gate)
            {
                if (state.IsQueued || state.IsProcessing)
                    count += 1;
            }
        }

        return count;
    }

    /// <summary>
    /// グローバル待ち行列を読み続け、execution 単位処理へ振り分けるワーカーループ。
    /// </summary>
    private async Task RunWorkerLoopAsync(CancellationToken stoppingToken)
    {
        // 単一 reader で global queue を取り出し、execution 単位処理へ委譲する。
        while (await _globalQueue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_globalQueue.Reader.TryRead(out var executionId))
            {
                if (!_states.TryGetValue(executionId, out var state))
                    continue;

                await ProcessExecutionAsync(executionId, state, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 単一 execution の pending 要求を処理する。
    /// processing 中に再通知が来た場合は 1 ループ追加して取りこぼしを防ぐ。
    /// </summary>
    private async Task ProcessExecutionAsync(Guid executionId, ExecutionQueueState state, CancellationToken stoppingToken)
    {
        lock (state.Gate)
        {
            if (state.IsProcessing)
                return;

            state.IsProcessing = true;
            state.IsQueued = false;
        }

        try
        {
            while (true)
            {
                // 同一 execution の短時間バーストをまとめる。
                await DelayForDebounceAsync(state, stoppingToken).ConfigureAwait(false);
                await UpdateProjectionAsync(executionId, stoppingToken).ConfigureAwait(false);

                lock (state.Gate)
                {
                    if (state.IsQueued)
                    {
                        // 処理中に追加通知が来たので、もう 1 周だけ続ける。
                        state.IsQueued = false;
                        continue;
                    }

                    // 完全に idle へ戻す。drain 待ちを解放。
                    state.ConsecutiveFailureCount = 0;
                    state.IsProcessing = false;
                    state.IdleSignal.TrySetResult(true);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            lock (state.Gate)
            {
                state.IsProcessing = false;
                state.IdleSignal.TrySetCanceled(stoppingToken);
            }
            throw;
        }
#pragma warning disable CA1031 // BackgroundService 投影キュー: 未取得例外も試行カウント・DLQ へ確実に集約する
        catch (Exception exception)
        {
            var retryDelayMs = 0;
            var shouldRetry = false;
            var retryCount = 0;

            lock (state.Gate)
            {
                state.ConsecutiveFailureCount += 1;
                retryCount = state.ConsecutiveFailureCount;
                state.IsProcessing = false;

                if (state.ConsecutiveFailureCount >= _maxRetryAttempts)
                {
                    state.IsQueued = false;
                    state.IsDeadLettered = true;
                    state.IdleSignal.TrySetResult(true);
                }
                else
                {
                    state.IsQueued = true;
                    shouldRetry = true;
                    retryDelayMs = GetRetryDelayMs(state.ConsecutiveFailureCount, _retryBaseDelayMs, _retryMaxDelayMs);
                    if (state.IdleSignal.Task.IsCompleted)
                        state.IdleSignal = ExecutionQueueState.CreatePendingSignal();
                }
            }

            if (!shouldRetry)
            {
                var deadLetterEntry = new DeadLetterEntry
                {
                    ExecutionId = executionId,
                    DeadLetteredAtUtc = DateTime.UtcNow,
                    RetryCount = retryCount,
                    ErrorType = exception.GetType().Name,
                    ErrorMessage = exception.Message
                };
                // NOTE: 現状はプロセス内メモリへ退避するのみ。永続 DLQ（DB/外部キュー）は未対応。
                _deadLetters[executionId] = deadLetterEntry;

                _logger.MovedToDeadLetter(exception, executionId, retryCount);
                return;
            }

            _logger.ProcessingFailedRetryScheduled(
                exception,
                executionId,
                retryCount,
                _maxRetryAttempts,
                retryDelayMs);

            await ScheduleRetryAsync(executionId, retryDelayMs, stoppingToken).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 最終 enqueue 時刻に基づくデバウンス待機を行う。
    /// </summary>
    private async Task DelayForDebounceAsync(ExecutionQueueState state, CancellationToken ct)
    {
        if (_debounceMs <= 0)
            return;

        DateTime dueAtUtc;
        lock (state.Gate)
        {
            dueAtUtc = state.LastEnqueuedAtUtc.AddMilliseconds(_debounceMs);
        }

        var delay = dueAtUtc - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// execution の現在エンジン状態を読み、投影更新を実行する。
    /// </summary>
    private async Task UpdateProjectionAsync(Guid executionId, CancellationToken ct)
    {
        // BackgroundService は singleton なので、毎回 scope を切って scoped service を解決する。
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var platformDataAccess = services.GetRequiredService<IPlatformDataAccess>();
        var tenantContextAccessor = services.GetRequiredService<ITenantContextAccessor>();
        var executionService = services.GetRequiredService<IExecutionService>();

        var tenantLookup = await platformDataAccess
            .FindExecutionTenantAsync(executionId, ct)
            .ConfigureAwait(false);
        if (tenantLookup is null)
        {
            _logger.ExecutionTenantNotFound(executionId);
            return;
        }

        var tenantState = new TenantContextState(
            tenantLookup.TenantId,
            tenantLookup.TenantKey,
            PrincipalId: null,
            tenantLookup.Lifecycle);

        await TenantExecutionScope
            .RunAsync(
                tenantContextAccessor,
                tenantState,
                () => executionService.UpdateProjectionFromEngineAsync(executionId, ct))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// バックオフ待機後に同一 execution をキューへ再投入する。
    /// </summary>
    private async Task ScheduleRetryAsync(Guid executionId, int delayMs, CancellationToken ct)
    {
        if (delayMs > 0)
            await Task.Delay(delayMs, ct).ConfigureAwait(false);

        if (!_states.TryGetValue(executionId, out var state))
            return;

        lock (state.Gate)
        {
            if (state.IsDeadLettered || state.IsProcessing)
                return;
        }

        await _globalQueue.Writer.WriteAsync(executionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 連続失敗回数に基づく指数バックオフ遅延（ms）を計算する。
    /// </summary>
    private static int GetRetryDelayMs(int failureCount, int baseMs, int maxMs)
    {
        if (baseMs <= 0)
            return 0;

        var exponent = Math.Max(0, failureCount - 1);
        var growth = Math.Pow(2, exponent);
        var delay = (int)Math.Min(int.MaxValue, baseMs * growth);
        return Math.Min(maxMs, Math.Max(baseMs, delay));
    }

}
