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
        // drain 待ちの同期に使う。idle になったら完了させる。
        internal TaskCompletionSource<bool> IdleSignal { get; set; } = CreateCompletedSignal();
    }

    private readonly Channel<Guid> _globalQueue;
    private readonly ConcurrentDictionary<Guid, WorkflowQueueState> _states = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<WorkflowProjectionUpdateQueueService> _logger;
    private readonly int _debounceMs;

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

        _scopeFactory = scopeFactory;
        _workflowEngine = workflowEngine;
        _logger = logger;
        _debounceMs = queueOptions.ProjectionFlushDebounceMs;

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
            _logger.LogError(exception, "Projection queue processing failed for workflow {WorkflowId}", workflowId);
            lock (state.Gate)
            {
                state.IsProcessing = false;
                state.IsQueued = true;
                if (state.IdleSignal.Task.IsCompleted)
                    state.IdleSignal = CreatePendingSignal();
            }
            // 失敗しても workflow 単位で再投入して再試行する。
            await _globalQueue.Writer.WriteAsync(workflowId, stoppingToken).ConfigureAwait(false);
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
