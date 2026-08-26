using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Security;
using Statevia.Runtime.Configuration;

namespace Statevia.Runtime.Services;

/// <summary>
/// 永続 <c>execution_work_items</c> を lease して実行するワーカー。
/// </summary>
/// <remarks>
/// <para>
/// ライフサイクル（Start / Resume）は <see cref="WorkerRuntimeOptions.MaxConcurrency"/> まで並列する。
/// Cancel は独立ループ。処理中は work item lease と checkpoint 所有 lease を heartbeat 延長する。
/// </para>
/// <para>同一プロセス所有中の Cancel はローカル process CTS をキャンセルして協調停止する。</para>
/// </remarks>
/// <param name="scopeFactory">スロットごとの DI スコープを作る。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="idGenerator">lease owner 識別子用。</param>
/// <param name="workerOptions">並列度と watchdog 閾値。</param>
public sealed class ExecutionWorkItemWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExecutionWorkItemWorkerHostedService> logger,
    IIdGenerator idGenerator,
    IOptions<WorkerRuntimeOptions> workerOptions) : BackgroundService
{
    private const string WorkerCommandMethod = "WORKER";
    private const string WorkerCommandPath = "/internal/execution-work-items";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly IReadOnlyList<string> LifecycleKinds =
        [ExecutionWorkItemKinds.Start, ExecutionWorkItemKinds.Resume];
    private static readonly IReadOnlyList<string> CancelKinds = [ExecutionWorkItemKinds.Cancel];
    private static readonly IReadOnlySet<string> WorkerPermissions =
        new HashSet<string>(StringComparer.Ordinal) { WellKnownPermissionKeys.ExecutionsWrite };
    private readonly string _leaseOwner = $"{Environment.MachineName}:{idGenerator.NewRandomGuid():N}";
    private readonly WorkerRuntimeOptions _worker = workerOptions.Value;
    private readonly LocalOwnedExecutionRegistry _ownedRegistry = new();
    private int _activeLifecycleSlots;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifecycle = RunPoolAsync(
            LifecycleKinds,
            _worker.MaxConcurrency,
            ProcessLifecycleSlotAsync,
            stoppingToken);
        var cancel = RunPoolAsync(
            CancelKinds,
            _worker.CancelConcurrency,
            ProcessCancelSlotAsync,
            stoppingToken);
        await Task.WhenAll(lifecycle, cancel).ConfigureAwait(false);
    }

    /// <summary>kind 絞り込み付きの claim プール。スロットごとに専用 scope で処理する。</summary>
    private async Task RunPoolAsync(
        IReadOnlyList<string> kinds,
        int maxConcurrency,
        Func<ExecutionWorkItemRow, CancellationToken, Task> process,
        CancellationToken stoppingToken)
    {
        var inFlight = new ConcurrentDictionary<Guid, Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RemoveCompleted(inFlight);
                var free = maxConcurrency - inFlight.Count;
                if (free <= 0)
                {
                    await Task.WhenAny(inFlight.Values).WaitAsync(stoppingToken).ConfigureAwait(false);
                    continue;
                }

                IReadOnlyList<ExecutionWorkItemRow> items;
                using (var scope = scopeFactory.CreateScope())
                {
                    var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
                    items = await queue.ClaimAsync(
                            _leaseOwner,
                            DateTime.UtcNow,
                            LeaseDuration,
                            free,
                            kinds,
                            stoppingToken)
                        .ConfigureAwait(false);
                }

                if (items.Count == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var item in items)
                {
                    var captured = item;
                    inFlight[captured.WorkItemId] = process(captured, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // バックグラウンドポーリングは反復継続のため例外種別を問わず捕捉する
            catch (Exception exception)
            {
                logger.WorkerIterationFailed(exception);
                await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
            }
#pragma warning restore CA1031
        }
    }

    private static void RemoveCompleted(ConcurrentDictionary<Guid, Task> inFlight)
    {
        foreach (var pair in inFlight)
        {
            if (pair.Value.IsCompleted)
                inFlight.TryRemove(pair.Key, out _);
        }
    }

    private async Task ProcessLifecycleSlotAsync(ExecutionWorkItemRow item, CancellationToken stoppingToken)
    {
        var active = Interlocked.Increment(ref _activeLifecycleSlots);
        logger.WorkerLifecycleSlotsChanged(active, _worker.MaxConcurrency);
        try
        {
            await ProcessAsync(
                    item,
                    registerLocalOwnership: true,
                    awaitLoad: true,
                    stoppingToken)
                .ConfigureAwait(false);
        }
        finally
        {
            var remaining = Interlocked.Decrement(ref _activeLifecycleSlots);
            logger.WorkerLifecycleSlotsChanged(remaining, _worker.MaxConcurrency);
        }
    }

    private Task ProcessCancelSlotAsync(ExecutionWorkItemRow item, CancellationToken stoppingToken)
    {
        if (_ownedRegistry.TryCancelLocal(item.ExecutionId))
            return CompleteLocalCancelAsync(item, stoppingToken);

        return ProcessAsync(
            item,
            registerLocalOwnership: false,
            awaitLoad: false,
            stoppingToken);
    }

    private async Task CompleteLocalCancelAsync(ExecutionWorkItemRow item, CancellationToken stoppingToken)
    {
        logger.WorkerLocalCancelInterrupt(item.ExecutionId);
        using var scope = scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
        await queue.CompleteAsync(item.WorkItemId, _leaseOwner, stoppingToken).ConfigureAwait(false);
    }

    private async Task ProcessAsync(
        ExecutionWorkItemRow item,
        bool registerLocalOwnership,
        bool awaitLoad,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var queue = scopedServices.GetRequiredService<IExecutionWorkQueue>();
        var processCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatCts = new CancellationTokenSource();
        IExecutionService? executions = null;
        var sessionStarted = false;
        var registered = false;
        try
        {
            try
            {
                var platformData = scopedServices.GetRequiredService<IPlatformDataAccess>();
                var tenant = await platformData.FindExecutionTenantAsync(item.ExecutionId, processCts.Token)
                    .ConfigureAwait(false);
                if (tenant is null || tenant.Lifecycle != TenantLifecycle.Active)
                {
                    await queue.CompleteAsync(item.WorkItemId, _leaseOwner, processCts.Token).ConfigureAwait(false);
                    return;
                }

                var accessor = scopedServices.GetRequiredService<ITenantContextAccessor>();
                using (accessor.SetContext(new TenantContextState(
                    tenant.TenantId,
                    tenant.TenantKey,
                    PrincipalId: null,
                    tenant.Lifecycle,
                    WorkerPermissions)))
                {
                    executions = scopedServices.GetRequiredService<IExecutionService>();
                    var generation = await executions.BeginOwnedSessionAsync(
                            item.ExecutionId,
                            _leaseOwner,
                            LeaseDuration,
                            processCts.Token)
                        .ConfigureAwait(false);
                    if (generation is null)
                    {
                        logger.WorkItemOwnershipAcquireFailed(item.WorkItemId, item.ExecutionId);
                        await queue.ReleaseAsync(
                                item.WorkItemId,
                                _leaseOwner,
                                DateTime.UtcNow.Add(RetryDelay),
                                ct)
                            .ConfigureAwait(false);
                        return;
                    }

                    sessionStarted = true;
                    if (registerLocalOwnership)
                        registered = _ownedRegistry.TryRegister(item.ExecutionId, processCts);

                    var heartbeatTask = HeartbeatAsync(
                        queue,
                        executions,
                        item.WorkItemId,
                        item.ExecutionId,
                        processCts,
                        heartbeatCts.Token);
                    try
                    {
                        await ProcessItemAsync(executions, item, processCts.Token).ConfigureAwait(false);
                        if (awaitLoad
                            && item.Kind is ExecutionWorkItemKinds.Start or ExecutionWorkItemKinds.Resume)
                        {
                            await executions.AwaitLocalExecutionLoadAsync(
                                    item.ExecutionId,
                                    _worker.NoProgressTimeout,
                                    processCts.Token)
                                .ConfigureAwait(false);
                        }

                        await queue.CompleteAsync(item.WorkItemId, _leaseOwner, processCts.Token)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        await heartbeatCts.CancelAsync().ConfigureAwait(false);
                        await AwaitHeartbeatAsync(heartbeatTask).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                sessionStarted = await HandleProcessInterruptedAsync(
                        item,
                        queue,
                        executions,
                        ct)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.WorkItemFailed(exception, item.WorkItemId);
                await queue.ReleaseAsync(
                        item.WorkItemId,
                        _leaseOwner,
                        DateTime.UtcNow.Add(RetryDelay),
                        ct)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (registered)
                _ownedRegistry.Unregister(item.ExecutionId);

            if (sessionStarted && executions is not null)
            {
                try
                {
                    await executions.EndOwnedSessionAsync(item.ExecutionId, ct).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // セッション終了はベストエフォート。
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    logger.WorkItemSessionEndFailed(exception, item.WorkItemId);
                    await executions.AbandonLocalOwnedSessionAsync(item.ExecutionId).ConfigureAwait(false);
                }
            }

            processCts.Dispose();
            heartbeatCts.Dispose();
        }
    }

    /// <summary>
    /// process CTS キャンセルをローカル Cancel IRQ と heartbeat 喪失に振り分ける。
    /// </summary>
    /// <returns>所有セッションを finally で終了すべきとき <see langword="true"/>。</returns>
    private async Task<bool> HandleProcessInterruptedAsync(
        ExecutionWorkItemRow item,
        IExecutionWorkQueue queue,
        IExecutionService? executions,
        CancellationToken ct)
    {
        if (_ownedRegistry.TryConsumeLocalCancel(item.ExecutionId) && executions is not null)
        {
            logger.WorkerLocalCancelInterrupt(item.ExecutionId);
            await executions.CancelAsync(
                    item.ExecutionId.ToString("D"),
                    idempotencyKey: null,
                    CreateWorkerCommandContext(),
                    ct)
                .ConfigureAwait(false);
            await queue.CompleteAsync(item.WorkItemId, _leaseOwner, ct).ConfigureAwait(false);
            return true;
        }

        logger.WorkItemLeaseLost(item.WorkItemId);
        if (executions is not null)
            await executions.AbandonLocalOwnedSessionAsync(item.ExecutionId).ConfigureAwait(false);
        return false;
    }

    /// <summary>work item と checkpoint 所有の lease を周期延長する。失敗時は処理 CTS をキャンセルする。</summary>
    private async Task HeartbeatAsync(
        IExecutionWorkQueue queue,
        IExecutionService executions,
        Guid workItemId,
        Guid executionId,
        CancellationTokenSource processCts,
        CancellationToken heartbeatCt)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(heartbeatCt).ConfigureAwait(false))
            {
                var workItemRenewed = await queue.RenewLeaseAsync(
                        workItemId,
                        _leaseOwner,
                        DateTime.UtcNow,
                        LeaseDuration,
                        heartbeatCt)
                    .ConfigureAwait(false);
                var ownershipRenewed = await executions.RenewOwnedSessionLeaseAsync(
                        executionId,
                        LeaseDuration,
                        heartbeatCt)
                    .ConfigureAwait(false);
                if (workItemRenewed && ownershipRenewed)
                    continue;

                await processCts.CancelAsync().ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (heartbeatCt.IsCancellationRequested)
        {
            // 正常停止（処理完了またはホスト停止）。
        }
#pragma warning disable CA1031 // heartbeat 自体の失敗は処理側へ伝播し、lease 喪失として扱う
        catch (Exception exception)
        {
            logger.WorkItemHeartbeatFailed(exception, workItemId);
            await processCts.CancelAsync().ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>heartbeat タスクの完了を待ち、キャンセル以外の例外は握りつぶさない。</summary>
    private static async Task AwaitHeartbeatAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 停止時の想定経路。
        }
    }

    /// <summary>Worker が Engine 操作に付ける CommandRequestContext。</summary>
    private static CommandRequestContext CreateWorkerCommandContext() =>
        new(WorkerCommandMethod, WorkerCommandPath);

    private static Task ProcessItemAsync(IExecutionService executions, ExecutionWorkItemRow item, CancellationToken ct) =>
        item.Kind switch
        {
            ExecutionWorkItemKinds.Resume =>
                ResumeAsync(executions, item, ct),
            ExecutionWorkItemKinds.Cancel =>
                executions.CancelAsync(
                    item.ExecutionId.ToString("D"),
                    idempotencyKey: null,
                    CreateWorkerCommandContext(),
                    ct),
            ExecutionWorkItemKinds.Start =>
                StartAsync(executions, item, ct),
            _ => throw new InvalidOperationException($"Unknown execution work item kind '{item.Kind}'.")
        };

    private static Task ResumeAsync(IExecutionService executions, ExecutionWorkItemRow item, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ExecutionResumeWorkItemPayload>(
                item.Payload,
                ExecutionWorkItemPayloadJson.Options)
            ?? throw new InvalidOperationException("Resume work item payload is invalid.");

        if (string.Equals(payload.Mode, ExecutionResumeWorkItemModes.Recovery, StringComparison.Ordinal))
        {
            return executions.RecoverExecutionAsync(
                item.ExecutionId,
                CreateWorkerCommandContext(),
                ct);
        }

        if (!string.Equals(payload.Mode, ExecutionResumeWorkItemModes.Event, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unknown resume mode '{payload.Mode}'.");

        if (string.IsNullOrWhiteSpace(payload.NodeId) || string.IsNullOrWhiteSpace(payload.EventName))
            throw new InvalidOperationException("Event resume payload requires nodeId and eventName.");

        return executions.ResumeNodeAsync(
            item.ExecutionId.ToString("D"),
            payload.NodeId,
            payload.EventName,
            payload.IdempotencyKey,
            CreateWorkerCommandContext(),
            ct);
    }

    private static Task<ExecutionResponse> StartAsync(
        IExecutionService executions,
        ExecutionWorkItemRow item,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ExecutionStartWorkItemPayload>(
                item.Payload,
                ExecutionWorkItemPayloadJson.Options)
            ?? throw new InvalidOperationException("Start work item payload is invalid.");
        ArgumentNullException.ThrowIfNull(payload.Request);
        return executions.ExecuteQueuedStartAsync(payload.ExecutionId, payload.Request, ct);
    }
}
