using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Services;

/// <summary>
/// 永続 <c>execution_work_items</c> を lease して実行するワーカー。
/// </summary>
/// <remarks>
/// <para>
/// 処理中は work item lease と checkpoint 所有 lease を heartbeat 延長する。
/// いずれかが失敗したらローカル実行を即停止し、Unload する。
/// </para>
/// </remarks>
internal sealed class ExecutionWorkItemWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExecutionWorkItemWorkerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int ClaimLimit = 1;
    private static readonly IReadOnlySet<string> WorkerPermissions =
        new HashSet<string>(StringComparer.Ordinal) { WellKnownPermissionKeys.ExecutionsWrite };
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IExecutionWorkQueue>();
                var items = await queue.ClaimAsync(
                    _leaseOwner,
                    DateTime.UtcNow,
                    LeaseDuration,
                    ClaimLimit,
                    stoppingToken).ConfigureAwait(false);

                foreach (var item in items)
                    await ProcessAsync(scope.ServiceProvider, queue, item, stoppingToken).ConfigureAwait(false);

                if (items.Count == 0)
                    await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
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

    private async Task ProcessAsync(
        IServiceProvider scopedServices,
        IExecutionWorkQueue queue,
        ExecutionWorkItemRow item,
        CancellationToken ct)
    {
        var processCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatCts = new CancellationTokenSource();
        IExecutionService? executions = null;
        var sessionStarted = false;
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
                // heartbeat 失敗による lease 喪失。他ワーカーへ譲るため Release しない。
                logger.WorkItemLeaseLost(item.WorkItemId);
                if (executions is not null)
                    await executions.AbandonLocalOwnedSessionAsync(item.ExecutionId).ConfigureAwait(false);
                sessionStarted = false;
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

    private static Task ProcessItemAsync(IExecutionService executions, ExecutionWorkItemRow item, CancellationToken ct) =>
        item.Kind switch
        {
            ExecutionWorkItemKinds.Resume =>
                ResumeAsync(executions, item, ct),
            ExecutionWorkItemKinds.Cancel =>
                executions.CancelAsync(
                    item.ExecutionId.ToString("D"),
                    idempotencyKey: null,
                    new CommandRequestContext("WORKER", "/internal/execution-work-items"),
                    ct),
            ExecutionWorkItemKinds.Start =>
                StartAsync(executions, item, ct),
            _ => throw new InvalidOperationException($"Unknown execution work item kind '{item.Kind}'.")
        };

    private static Task ResumeAsync(IExecutionService executions, ExecutionWorkItemRow item, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ExecutionResumeWorkItemPayload>(item.Payload)
            ?? throw new InvalidOperationException("Resume work item payload is invalid.");

        if (string.Equals(payload.Mode, ExecutionResumeWorkItemModes.Recovery, StringComparison.Ordinal))
        {
            return executions.RecoverExecutionAsync(
                item.ExecutionId,
                new CommandRequestContext("WORKER", "/internal/execution-work-items"),
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
            new CommandRequestContext("WORKER", "/internal/execution-work-items"),
            ct);
    }

    private static Task<ExecutionResponse> StartAsync(
        IExecutionService executions,
        ExecutionWorkItemRow item,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ExecutionStartWorkItemPayload>(item.Payload)
            ?? throw new InvalidOperationException("Start work item payload is invalid.");
        return executions.ExecuteQueuedStartAsync(payload.ExecutionId, payload.Request, ct);
    }
}
