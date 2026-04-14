using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Services;

/// <summary>
/// ノード完了通知を受け、ワークフロー投影更新を逐次処理するキューサービス。
/// </summary>
public sealed class WorkflowProjectionUpdateQueueService : BackgroundService, IWorkflowProjectionUpdateQueue
{
    /// <summary>
    /// 再試行上限に達して退避したワークフロー情報。
    /// </summary>
    private sealed class DeadLetterEntry
    {
        internal required Guid WorkflowId { get; init; }
        internal required DateTime DeadLetteredAtUtc { get; init; }
        internal required int RetryCount { get; init; }
        internal required string ErrorType { get; init; }
        internal required string ErrorMessage { get; init; }
    }

    /// <summary>
    /// workflow 単位の局所状態。
    /// </summary>
    private sealed class WorkflowQueueState
    {
        // 同一 workflow の状態遷移はこの lock で直列化する。
        internal object Gate { get; } = new();
        // キュー投入済み（または再投入予約あり）を示すフラグ。
        internal bool IsQueued { get; set; }
        // 現在ワーカーが当該 workflow を処理中かどうか。
        internal bool IsProcessing { get; set; }
        // デバウンス判定用の最終 enqueue 時刻。
        internal DateTime LastEnqueuedAtUtc { get; set; } = DateTime.UtcNow;
        // 連続失敗回数（成功で 0 に戻す）。
        internal int ConsecutiveFailureCount { get; set; }
        // dead-letter 退避済みかどうか。
        internal bool IsDeadLettered { get; set; }
        // drain 待ちの同期に使う。idle になったら完了させる。
        internal TaskCompletionSource<bool> IdleSignal { get; set; } = CreateCompletedSignal();
    }

    private readonly Channel<Guid> _globalQueue;
    private readonly ConcurrentDictionary<Guid, WorkflowQueueState> _states = new();
    private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _deadLetters = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<WorkflowProjectionUpdateQueueService> _logger;
    private readonly int _debounceMs;
    private readonly int _maxRetryAttempts;
    private readonly int _retryBaseDelayMs;
    private readonly int _retryMaxDelayMs;

    public WorkflowProjectionUpdateQueueService(
        IServiceScopeFactory scopeFactory,
        IWorkflowEngine workflowEngine,
        IOptions<WorkflowProjectionQueueOptions> options,
        ILogger<WorkflowProjectionUpdateQueueService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var queueOptions = options.Value;

        if (queueOptions.MaxGlobalQueueSize < 1)
            throw new ArgumentException("WorkflowProjectionQueue:MaxGlobalQueueSize must be >= 1");
        if (queueOptions.ProjectionFlushDebounceMs is < 0 or > 250)
            throw new ArgumentException("WorkflowProjectionQueue:ProjectionFlushDebounceMs must be between 0 and 250");
        if (queueOptions.MaxRetryAttempts < 1)
            throw new ArgumentException("WorkflowProjectionQueue:MaxRetryAttempts must be >= 1");
        if (queueOptions.RetryBaseDelayMs < 0)
            throw new ArgumentException("WorkflowProjectionQueue:RetryBaseDelayMs must be >= 0");
        if (queueOptions.RetryMaxDelayMs < queueOptions.RetryBaseDelayMs)
            throw new ArgumentException("WorkflowProjectionQueue:RetryMaxDelayMs must be >= RetryBaseDelayMs");

        _scopeFactory = scopeFactory;
        _workflowEngine = workflowEngine;
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
    public async Task EnqueueAsync(Guid workflowId, CancellationToken ct)
    {
        var state = _states.GetOrAdd(workflowId, _ => new WorkflowQueueState());
        var shouldWrite = false;

        lock (state.Gate)
        {
            if (state.IsDeadLettered)
            {
                // dead-letter 済みの workflow は自動再開しない（手動オペレーション対象）。
                _logger.LogWarning("Skip enqueue because workflow is dead-lettered WorkflowId={WorkflowId}", workflowId);
                return;
            }

            state.LastEnqueuedAtUtc = DateTime.UtcNow;
            if (!state.IsQueued && !state.IsProcessing)
            {
                // idle -> active 遷移。drain 待ちが同期できるよう pending signal を作り直す。
                if (state.IdleSignal.Task.IsCompleted)
                    state.IdleSignal = CreatePendingSignal();
                state.IsQueued = true;
                shouldWrite = true;
            }
            else
            {
                // 既に queue/processing 中なら、workflow 単位 1 スロットに併合する。
                state.IsQueued = true;
            }
        }

        if (shouldWrite)
            await _globalQueue.Writer.WriteAsync(workflowId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DrainAsync(Guid workflowId, CancellationToken ct)
    {
        if (!_states.TryGetValue(workflowId, out var state))
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
        // Engine からの通知は workflowId(string) で届くので Guid へ変換して queue へ流す。
        _workflowEngine.SetNodeCompletedHandler(workflowId =>
        {
            if (!Guid.TryParse(workflowId, out var parsedWorkflowId))
            {
                return Task.CompletedTask;
            }

            return EnqueueAsync(parsedWorkflowId, stoppingToken);
        });

        return RunWorkerLoopAsync(stoppingToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _workflowEngine.SetNodeCompletedHandler(null);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// グローバル待ち行列を読み続け、workflow 単位処理へ振り分けるワーカーループ。
    /// </summary>
    private async Task RunWorkerLoopAsync(CancellationToken stoppingToken)
    {
        // 単一 reader で global queue を取り出し、workflow 単位処理へ委譲する。
        while (await _globalQueue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_globalQueue.Reader.TryRead(out var workflowId))
            {
                if (!_states.TryGetValue(workflowId, out var state))
                    continue;

                await ProcessWorkflowAsync(workflowId, state, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 単一 workflow の pending 要求を処理する。
    /// processing 中に再通知が来た場合は 1 ループ追加して取りこぼしを防ぐ。
    /// </summary>
    private async Task ProcessWorkflowAsync(Guid workflowId, WorkflowQueueState state, CancellationToken stoppingToken)
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
                // 同一 workflow の短時間バーストをまとめる。
                await DelayForDebounceAsync(state, stoppingToken).ConfigureAwait(false);
                await UpdateProjectionAsync(workflowId, stoppingToken).ConfigureAwait(false);

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
                        state.IdleSignal = CreatePendingSignal();
                }
            }

            if (!shouldRetry)
            {
                var deadLetterEntry = new DeadLetterEntry
                {
                    WorkflowId = workflowId,
                    DeadLetteredAtUtc = DateTime.UtcNow,
                    RetryCount = retryCount,
                    ErrorType = exception.GetType().Name,
                    ErrorMessage = exception.Message
                };
                // NOTE: 現状はプロセス内メモリへ退避するのみ。永続 DLQ（DB/外部キュー）は未対応。
                _deadLetters[workflowId] = deadLetterEntry;

                _logger.LogError(
                    exception,
                    "Projection queue moved workflow to dead-letter WorkflowId={WorkflowId} RetryCount={RetryCount}",
                    workflowId,
                    retryCount);
                return;
            }

            _logger.LogWarning(
                exception,
                "Projection queue processing failed. Retry scheduled WorkflowId={WorkflowId} Attempt={Attempt}/{MaxAttempts} DelayMs={DelayMs}",
                workflowId,
                retryCount,
                _maxRetryAttempts,
                retryDelayMs);

            await ScheduleRetryAsync(workflowId, retryDelayMs, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 最終 enqueue 時刻に基づくデバウンス待機を行う。
    /// </summary>
    private async Task DelayForDebounceAsync(WorkflowQueueState state, CancellationToken ct)
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
    /// workflow の現在エンジン状態を読み、投影更新を実行する。
    /// </summary>
    private async Task UpdateProjectionAsync(Guid workflowId, CancellationToken ct)
    {
        // BackgroundService は singleton なので、毎回 scope を切って scoped service を解決する。
        using var scope = _scopeFactory.CreateScope();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        await workflowService.UpdateProjectionFromEngineAsync(workflowId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// バックオフ待機後に同一 workflow をキューへ再投入する。
    /// </summary>
    private async Task ScheduleRetryAsync(Guid workflowId, int delayMs, CancellationToken ct)
    {
        if (delayMs > 0)
            await Task.Delay(delayMs, ct).ConfigureAwait(false);

        if (!_states.TryGetValue(workflowId, out var state))
            return;

        lock (state.Gate)
        {
            if (state.IsDeadLettered || state.IsProcessing)
                return;
        }

        await _globalQueue.Writer.WriteAsync(workflowId, ct).ConfigureAwait(false);
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

    /// <summary>
    /// drain 待機用の未完了シグナルを生成する。
    /// </summary>
    private static TaskCompletionSource<bool> CreatePendingSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 初期状態（idle）を表す完了済みシグナルを生成する。
    /// </summary>
    private static TaskCompletionSource<bool> CreateCompletedSignal()
    {
        var taskCompletionSource = CreatePendingSignal();
        taskCompletionSource.TrySetResult(true);
        return taskCompletionSource;
    }
}
