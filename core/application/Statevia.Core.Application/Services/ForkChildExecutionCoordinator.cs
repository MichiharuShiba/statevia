using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// Hosted Runtime の Fork を親＋子 execution に展開する。
/// </summary>
/// <remarks>
/// <para>子作成・execution_branches・Start enqueue を同一 ReadCommitted で行い、一時失敗は Options に従い再試行する（D9）。</para>
/// <para>上限超過時は作成済みの未終端子へ Cancel を enqueue し、親を Failed にする。</para>
/// <para>親 Cancel カスケードと分岐 Wait の子配送解決（要件4）も担う。</para>
/// <para>親 graph 読み取り時の論理 Fork/Join 合成（D6）と、イベント合成用の子孫列挙（D6.1）も担う。</para>
/// </remarks>
/// <param name="executor">トランザクション実行。</param>
/// <param name="executions">executions 永続化。</param>
/// <param name="branches">execution_branches 永続化。</param>
/// <param name="waits">execution_waits 永続化（配送先解決）。</param>
/// <param name="workQueue">work item キュー。</param>
/// <param name="idGenerator">ID 生成。</param>
/// <param name="displayIdWrites">display ID 割当。</param>
/// <param name="eventStore">イベントストア。</param>
/// <param name="options">展開リトライ設定。</param>
/// <param name="logger">ログ。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
public sealed class ForkChildExecutionCoordinator(
    ICoreTransactionExecutor executor,
    IExecutionRepository executions,
    IExecutionBranchRepository branches,
    IExecutionWaitRepository waits,
    IExecutionWorkQueue workQueue,
    IIdGenerator idGenerator,
    IDisplayIdWriteService displayIdWrites,
    IEventStoreRepository eventStore,
    IOptions<ForkChildExpansionOptions> options,
    ILogger<ForkChildExecutionCoordinator> logger) : IForkChildExecutionCoordinator
{
    private readonly ForkChildExpansionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<ForkExpansionResult> ExpandForkAsync(ForkExpansionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Branches);
        if (request.Branches.Count == 0)
            throw new ArgumentException("Fork branches must not be empty.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ForkNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DefinitionDisplayId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SecuritySnapshotJson);
        ArgumentNullException.ThrowIfNull(request.CompiledDefinition);
        if (_options.MaxAttempts < 1)
            throw new InvalidOperationException("ForkChildExpansionOptions.MaxAttempts must be >= 1.");

        var joinState = ResolveJoinState(request.CompiledDefinition, request.Branches);

        Exception? lastError = null;
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                var childIds = await TryExpandOnceAsync(request, joinState, ct).ConfigureAwait(false);
                return new ForkExpansionResult(Succeeded: true, childIds, joinState);
            }
#pragma warning disable CA1031 // 展開一時失敗は D9 どおり再試行し、上限後に親 Failed へ倒す
            catch (Exception ex) when (ex is not ForkJoinResolutionException and not OperationCanceledException)
#pragma warning restore CA1031
            {
                lastError = ex;
                logger.ForkExpansionAttemptFailed(
                    ex,
                    attempt,
                    _options.MaxAttempts,
                    request.ParentExecutionId,
                    request.ForkNodeId);

                if (attempt >= _options.MaxAttempts)
                    break;

                var delayMs = Math.Max(0, _options.BaseDelayMs) * attempt;
                if (delayMs > 0)
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
        }

        await FailParentAfterExhaustionAsync(request, joinState, lastError, ct).ConfigureAwait(false);
        return new ForkExpansionResult(Succeeded: false, Array.Empty<Guid>(), joinState);
    }

    /// <inheritdoc />
    public async Task<bool> NotifyChildTerminalAsync(
        Guid childExecutionId,
        string status,
        string? outputJson,
        string? statesJson,
        string? varsJson,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!ExecutionProjectionStatuses.IsTerminal(status))
            throw new ArgumentException($"Status '{status}' is not terminal.", nameof(status));

        var now = DateTime.UtcNow;
        return await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var branch = await branches.GetByChildExecutionIdAsync(uow, childExecutionId, innerCt)
                        .ConfigureAwait(false);
                    if (branch is null)
                        return false;

                    if (string.Equals(branch.Status, ExecutionBranchStatuses.Running, StringComparison.Ordinal))
                    {
                        var updated = await branches.TryUpdateStatusAsync(
                                uow,
                                branch.ParentExecutionId,
                                branch.ForkNodeId,
                                branch.BranchState,
                                new ExecutionBranchStatusUpdate(
                                    status,
                                    now,
                                    outputJson,
                                    statesJson,
                                    varsJson),
                                innerCt)
                            .ConfigureAwait(false);
                        if (!updated)
                            return false;
                    }
                    else if (!ExecutionProjectionStatuses.IsTerminal(branch.Status))
                    {
                        return false;
                    }

                    var resumePayload = JsonSerializer.Serialize(
                        new ExecutionResumeWorkItemPayload(
                            ExecutionResumeWorkItemModes.Event,
                            branch.ForkNodeId,
                            ExecutionWaitEventNames.ChildCompleted),
                        ExecutionWorkItemPayloadJson.Options);

                    await workQueue.EnqueueAsync(
                            uow,
                            new ExecutionWorkItemRow
                            {
                                WorkItemId = idGenerator.NewSequentialGuid(),
                                ExecutionId = branch.ParentExecutionId,
                                Kind = ExecutionWorkItemKinds.Resume,
                                Payload = resumePayload,
                                AvailableAt = now,
                                Attempts = 0,
                                CreatedAt = now
                            },
                            innerCt)
                        .ConfigureAwait(false);

                    logger.ForkChildTerminalSignaled(
                        childExecutionId,
                        branch.ParentExecutionId,
                        branch.ForkNodeId,
                        status);
                    return true;
                },
                ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ForkJoinEvaluation> EvaluateJoinAsync(
        Guid parentExecutionId,
        string forkNodeId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(forkNodeId);

        var rows = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => branches.ListByParentAndForkNodeAsync(
                    uow,
                    parentExecutionId,
                    forkNodeId,
                    innerCt),
                ct)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"No execution_branches for parent {parentExecutionId:D} forkNodeId '{forkNodeId}'.");
        }

        var joinState = rows[0].JoinState;

        var failed = rows.FirstOrDefault(r =>
            string.Equals(r.Status, ExecutionBranchStatuses.Failed, StringComparison.Ordinal)
            || string.Equals(r.Status, ExecutionBranchStatuses.Cancelled, StringComparison.Ordinal));
        if (failed is not null)
        {
            // 論理 Join と同様、一文失敗は未終端の兄弟を待たずに親へ伝播する。
            return new ForkJoinEvaluation(
                ForkJoinEvaluationKind.Failed,
                joinState,
                FailureStatus: failed.Status);
        }

        if (rows.Any(r => string.Equals(r.Status, ExecutionBranchStatuses.Running, StringComparison.Ordinal)))
        {
            return new ForkJoinEvaluation(ForkJoinEvaluationKind.Waiting, joinState);
        }

        if (!rows.All(r => string.Equals(r.Status, ExecutionBranchStatuses.Completed, StringComparison.Ordinal)))
        {
            return new ForkJoinEvaluation(ForkJoinEvaluationKind.Waiting, joinState);
        }

        var candidateInputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var contextMerges = new List<ForkJoinChildContextMerge>();
        foreach (var row in rows.OrderBy(r => r.UpdatedAt).ThenBy(r => r.BranchState, StringComparer.Ordinal))
        {
            candidateInputs[row.BranchState] = ParseOutputJson(row.OutputJson);
            contextMerges.Add(new ForkJoinChildContextMerge(
                ParseStatesJson(row.StatesJson),
                ParseOutputJson(row.VarsJson)));
        }

        return new ForkJoinEvaluation(
            ForkJoinEvaluationKind.Satisfied,
            joinState,
            CandidateInputs: candidateInputs,
            ContextMerges: contextMerges);
    }

    /// <inheritdoc />
    public async Task<bool> HasPendingPhysicalJoinBranchesAsync(
        Guid parentExecutionId,
        CancellationToken ct)
    {
        var rows = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => branches.ListByParentExecutionIdAsync(uow, parentExecutionId, innerCt),
                ct)
            .ConfigureAwait(false);

        return rows.Any(r =>
            string.Equals(r.Status, ExecutionBranchStatuses.Running, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async Task CascadeCancelToRunningChildrenAsync(Guid parentExecutionId, CancellationToken ct)
    {
        await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var rows = await branches.ListByParentExecutionIdAsync(uow, parentExecutionId, innerCt)
                        .ConfigureAwait(false);
                    await EnqueueCancelForRunningBranchesAsync(uow, rows, DateTime.UtcNow, innerCt)
                        .ConfigureAwait(false);
                },
                ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ForkWaitDeliveryTarget?> TryResolveChildWaitDeliveryAsync(
        Guid requestedExecutionId,
        string? nodeId,
        string eventName,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var normalizedEvent = eventName.Trim();

        return await executor.ExecuteReadOnlyAsync(
                async (uow, innerCt) =>
                {
                    var localWaits = await waits.ListByExecutionIdAsync(uow, requestedExecutionId, innerCt)
                        .ConfigureAwait(false);
                    if (FindMatchingWaits(localWaits, requestedExecutionId, nodeId, normalizedEvent).Count > 0)
                        return null;

                    var childRows = await branches.ListByParentExecutionIdAsync(uow, requestedExecutionId, innerCt)
                        .ConfigureAwait(false);
                    if (childRows.Count == 0)
                        return null;

                    var matches = new List<ForkWaitDeliveryTarget>();
                    foreach (var childExecutionId in childRows.Select(child => child.ExecutionId))
                    {
                        var childWaits = await waits.ListByExecutionIdAsync(uow, childExecutionId, innerCt)
                            .ConfigureAwait(false);
                        matches.AddRange(
                            FindMatchingWaits(childWaits, childExecutionId, nodeId, normalizedEvent));
                    }

                    return matches.Count switch
                    {
                        0 => null,
                        1 => matches[0],
                        _ => throw new InvalidOperationException(
                            $"Ambiguous Wait delivery for execution {requestedExecutionId:D} event '{normalizedEvent}': "
                            + $"{matches.Count} child waits match.")
                    };
                },
                ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> ComposeReadModelGraphJsonAsync(
        Guid executionId,
        string baseGraphJson,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseGraphJson);

        var childRows = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => branches.ListByParentExecutionIdAsync(uow, executionId, innerCt),
                ct)
            .ConfigureAwait(false);

        if (childRows.Count == 0)
            return baseGraphJson;

        var composedBranches = new List<PhysicalForkGraphComposer.BranchGraph>(childRows.Count);
        foreach (var child in childRows)
        {
            var childSnapshot = await executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => executions.GetSnapshotByExecutionIdAsync(uow, child.ExecutionId, innerCt),
                    ct)
                .ConfigureAwait(false);
            var childBase = childSnapshot?.GraphJson ?? """{"nodes":[],"edges":[]}""";
            // ネスト Fork: 子自身も親役なら再帰合成してから親へ載せる。
            var childComposed = await ComposeReadModelGraphJsonAsync(child.ExecutionId, childBase, ct)
                .ConfigureAwait(false);
            // Join 辺は状態名ではなく、この Fork 訪問に対応する Join 到達 nodeId に固定する。
            var joinNodeId = PhysicalForkGraphComposer.ResolveJoinNodeId(
                    baseGraphJson,
                    child.ForkNodeId,
                    child.JoinState)
                ?? string.Empty;
            composedBranches.Add(
                new PhysicalForkGraphComposer.BranchGraph(
                    child.ForkNodeId,
                    joinNodeId,
                    child.BranchState,
                    childComposed));
        }

        return PhysicalForkGraphComposer.Compose(baseGraphJson, composedBranches);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListDescendantExecutionIdsAsync(
        Guid parentExecutionId,
        CancellationToken ct)
    {
        var result = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentExecutionId);
        var seen = new HashSet<Guid> { parentExecutionId };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childRows = await executor.ExecuteReadOnlyAsync(
                    (uow, innerCt) => branches.ListByParentExecutionIdAsync(uow, current, innerCt),
                    ct)
                .ConfigureAwait(false);

            foreach (var childId in childRows
                         .Select(r => r.ExecutionId)
                         .Where(id => seen.Add(id)))
            {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }

    /// <summary>
    /// Join.all と Fork 分岐先頭集合が一致する Join を一意に解決する。
    /// </summary>
    internal static string ResolveJoinState(
        CompiledWorkflowDefinition definition,
        IReadOnlyList<ForkBranchExpansion> branches)
    {
        var branchSet = new HashSet<string>(
            branches.Select(b => b.BranchState),
            StringComparer.OrdinalIgnoreCase);
        if (branchSet.Count != branches.Count)
            throw new ForkJoinResolutionException("Fork branch states must be unique.");

        var matches = definition.JoinTable
            .Where(pair =>
            {
                var joinDeps = new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase);
                return joinDeps.SetEquals(branchSet);
            })
            .Select(pair => pair.Key)
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ForkJoinResolutionException(
                $"No Join matches fork branches [{string.Join(", ", branchSet)}]."),
            _ => throw new ForkJoinResolutionException(
                $"Ambiguous Join for fork branches [{string.Join(", ", branchSet)}]: {string.Join(", ", matches)}.")
        };
    }

    private async Task<IReadOnlyList<Guid>> TryExpandOnceAsync(
        ForkExpansionRequest request,
        string joinState,
        CancellationToken ct)
    {
        var existing = await executor.ExecuteReadOnlyAsync(
                (uow, innerCt) => branches.ListByParentAndForkNodeAsync(
                    uow,
                    request.ParentExecutionId,
                    request.ForkNodeId,
                    innerCt),
                ct)
            .ConfigureAwait(false);

        if (existing.Count == request.Branches.Count
            && existing.All(b => string.Equals(b.JoinState, joinState, StringComparison.OrdinalIgnoreCase)))
        {
            // 展開済み（クラッシュ再入）: 二重 Start を避けるため enqueue しない。
            return existing
                .OrderBy(b => IndexOfBranch(request.Branches, b.BranchState))
                .Select(b => b.ExecutionId)
                .ToList();
        }

        var prepared = await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var now = DateTime.UtcNow;
                    var createdBranches = new List<ExecutionBranchRow>(request.Branches.Count);
                    var startItems = new List<ExecutionWorkItemRow>(request.Branches.Count);
                    const string emptyGraphJson = """{"nodes":[],"edges":[]}""";

                    foreach (var branch in request.Branches)
                    {
                        var childId = idGenerator.NewSequentialGuid();
                        await displayIdWrites
                            .AllocateAsync(uow, DisplayIdResourceTypes.Execution, childId, innerCt)
                            .ConfigureAwait(false);

                        var childRow = new ExecutionRow
                        {
                            ExecutionId = childId,
                            TenantId = request.TenantId,
                            DefinitionId = request.DefinitionId,
                            DefinitionVersionId = request.DefinitionVersionId,
                            Status = ExecutionProjectionStatuses.Running,
                            StartedAt = now,
                            UpdatedAt = now,
                            CancelRequested = false,
                            RestartLost = false,
                            SecuritySnapshotJson = request.SecuritySnapshotJson
                        };
                        var snapshotRow = new ExecutionGraphSnapshotRow
                        {
                            ExecutionId = childId,
                            GraphJson = emptyGraphJson,
                            UpdatedAt = now
                        };
                        await executions.AddExecutionAndSnapshotAsync(uow, childRow, snapshotRow, innerCt)
                            .ConfigureAwait(false);

                        var startedPayload = JsonSerializer.Serialize(
                            new
                            {
                                parentExecutionId = request.ParentExecutionId.ToString("D"),
                                forkNodeId = request.ForkNodeId,
                                branchState = branch.BranchState,
                                joinState,
                                definitionId = request.DefinitionId.ToString("D"),
                                definitionVersionId = request.DefinitionVersionId.ToString("D"),
                                definitionVersion = request.DefinitionVersion,
                                tenantId = request.TenantId
                            },
                            JsonSerializerProfiles.CamelCase);
                        await eventStore
                            .AppendAsync(uow, childId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                            .ConfigureAwait(false);

                        createdBranches.Add(new ExecutionBranchRow
                        {
                            ParentExecutionId = request.ParentExecutionId,
                            ExecutionId = childId,
                            ForkNodeId = request.ForkNodeId,
                            JoinState = joinState,
                            BranchState = branch.BranchState,
                            Status = ExecutionBranchStatuses.Running,
                            CreatedAt = now,
                            UpdatedAt = now
                        });

                        var startRequest = new StartExecutionRequest
                        {
                            DefinitionId = request.DefinitionDisplayId,
                            DefinitionVersionId = request.DefinitionVersionId,
                            DefinitionVersion = request.DefinitionVersion,
                            Input = ToJsonElement(branch.MappedInput),
                            InitialState = branch.BranchState
                        };
                        startItems.Add(new ExecutionWorkItemRow
                        {
                            WorkItemId = idGenerator.NewSequentialGuid(),
                            ExecutionId = childId,
                            Kind = ExecutionWorkItemKinds.Start,
                            Payload = JsonSerializer.Serialize(
                                new ExecutionStartWorkItemPayload(childId, startRequest),
                                ExecutionWorkItemPayloadJson.Options),
                            AvailableAt = now,
                            Attempts = 0,
                            CreatedAt = now
                        });
                    }

                    await branches.InsertBranchesIdempotentAsync(uow, createdBranches, innerCt)
                        .ConfigureAwait(false);
                    await workQueue.EnqueueManyAsync(uow, startItems, innerCt).ConfigureAwait(false);

                    return createdBranches.Select(b => b.ExecutionId).ToList();
                },
                ct)
            .ConfigureAwait(false);

        return prepared;
    }

    private static int IndexOfBranch(IReadOnlyList<ForkBranchExpansion> branches, string branchState)
    {
        for (var i = 0; i < branches.Count; i++)
        {
            if (string.Equals(branches[i].BranchState, branchState, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    private async Task FailParentAfterExhaustionAsync(
        ForkExpansionRequest request,
        string joinState,
        Exception? lastError,
        CancellationToken ct)
    {
        logger.ForkExpansionExhausted(
            lastError ?? new InvalidOperationException("Fork expansion exhausted with no captured exception."),
            request.ParentExecutionId,
            request.ForkNodeId);

        var now = DateTime.UtcNow;
        await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var rows = await branches.ListByParentAndForkNodeAsync(
                            uow,
                            request.ParentExecutionId,
                            request.ForkNodeId,
                            innerCt)
                        .ConfigureAwait(false);

                    var snapshot = await executions.GetSnapshotByExecutionIdAsync(
                            uow,
                            request.ParentExecutionId,
                            innerCt)
                        .ConfigureAwait(false);
                    var graphJson = snapshot?.GraphJson ?? """{"nodes":[],"edges":[]}""";

                    await executions
                        .UpdateExecutionAndSnapshotAsync(
                            uow,
                            request.ParentExecutionId,
                            ExecutionProjectionStatuses.Failed,
                            cancelRequested: true,
                            graphJson,
                            innerCt)
                        .ConfigureAwait(false);

                    await EnqueueCancelForRunningBranchesAsync(uow, rows, now, innerCt).ConfigureAwait(false);
                },
                ct)
            .ConfigureAwait(false);

        _ = joinState;
    }

    private async Task EnqueueCancelForRunningBranchesAsync(
        ICoreUnitOfWork uow,
        IReadOnlyList<ExecutionBranchRow> rows,
        DateTime now,
        CancellationToken ct)
    {
        var cancelItems = rows
            .Where(r => string.Equals(r.Status, ExecutionBranchStatuses.Running, StringComparison.Ordinal))
            .Select(row => new ExecutionWorkItemRow
            {
                WorkItemId = idGenerator.NewSequentialGuid(),
                ExecutionId = row.ExecutionId,
                Kind = ExecutionWorkItemKinds.Cancel,
                Payload = "{}",
                AvailableAt = now,
                Attempts = 0,
                CreatedAt = now
            })
            .ToList();

        if (cancelItems.Count == 0)
            return;

        await workQueue.EnqueueManyAsync(uow, cancelItems, ct).ConfigureAwait(false);
    }

    private static List<ForkWaitDeliveryTarget> FindMatchingWaits(
        IReadOnlyList<ExecutionWaitRow> waitRows,
        Guid executionId,
        string? nodeId,
        string eventName)
    {
        return waitRows
            .Where(wait =>
            {
                if (!string.IsNullOrWhiteSpace(nodeId)
                    && !string.Equals(wait.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return false;
                }

                return wait.AllowedEvents.Any(allowed =>
                    string.Equals(allowed, eventName, StringComparison.Ordinal));
            })
            .Select(wait => new ForkWaitDeliveryTarget(executionId, wait.NodeId, eventName))
            .ToList();
    }

    private static JsonElement? ToJsonElement(object? value)
    {
        if (value is null)
            return null;
        if (value is JsonElement element)
            return element.Clone();

        var json = JsonSerializer.Serialize(value, JsonSerializerProfiles.CamelCase);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement? ParseOutputJson(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
            return null;

        using var document = JsonDocument.Parse(outputJson);
        return document.RootElement.Clone();
    }

    private static Dictionary<string, object?> ParseStatesJson(string? statesJson)
    {
        if (string.IsNullOrWhiteSpace(statesJson))
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(statesJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return document.RootElement.EnumerateObject()
            .ToDictionary(
                prop => prop.Name,
                prop => (object?)prop.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);
    }
}
