using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行ライフサイクルコマンド（Start / Cancel / QueuedStart）。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。HTTP / Worker 契約は変更しない。</para>
/// <para>冪等・投影は既存の <see cref="ExecutionIdempotencyService"/> / <see cref="ExecutionProjectionOrchestrator"/> を利用する。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="displayIds">表示 ID 解決。</param>
/// <param name="compiler">定義コンパイル復元。</param>
/// <param name="idGenerator">ID 生成。</param>
/// <param name="executions">executions 永続化。</param>
/// <param name="definitions">定義リポジトリ。</param>
/// <param name="projectAuth">プロジェクト認可。</param>
/// <param name="runtimeAuth">Runtime 権限。</param>
/// <param name="mutationAuth">実行ミューテーション認可。</param>
/// <param name="snapshotFactory">セキュリティ snapshot。</param>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="dedup">command_dedup。</param>
/// <param name="eventStore">event_store。</param>
/// <param name="eventDeliveryDedup">event_delivery_dedup。</param>
/// <param name="displayIdWrites">display ID 割当。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="mutationPersistence">Serializable 永続化。</param>
/// <param name="logger">構造化ログ。</param>
/// <param name="checkpointStore">runtime checkpoint。</param>
/// <param name="ownership">Worker 所有トラッカー。</param>
/// <param name="idempotency">冪等サービス。</param>
/// <param name="projection">投影オーケストレータ。</param>
/// <param name="workQueue">work queue（任意）。</param>
/// <param name="forkChildCoordinator">Fork 協調（任意）。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S107:Methods should not have too many parameters",
    Justification = "DI による明示的コンストラクタ注入。")]
internal sealed class ExecutionLifecycleCommandService(
    IExecutionEngine engine,
    IDisplayIdService displayIds,
    IDefinitionCompilerService compiler,
    IIdGenerator idGenerator,
    IExecutionRepository executions,
    IDefinitionRepository definitions,
    IProjectAuthorizationService projectAuth,
    IRuntimePermissionAuthorization runtimeAuth,
    IExecutionMutationAuthorization mutationAuth,
    IExecutionSecuritySnapshotFactory snapshotFactory,
    ITenantContextAccessor tenantContext,
    ICommandDedupRepository dedup,
    IEventStoreRepository eventStore,
    IEventDeliveryDedupRepository eventDeliveryDedup,
    IDisplayIdWriteService displayIdWrites,
    ICoreTransactionExecutor executor,
    IExecutionMutationPersistence mutationPersistence,
    ILogger<ExecutionLifecycleCommandService> logger,
    IExecutionCheckpointStore checkpointStore,
    ExecutionOwnershipTracker ownership,
    ExecutionIdempotencyService idempotency,
    ExecutionProjectionOrchestrator projection,
    IExecutionWorkQueue? workQueue = null,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    /// <summary>HTTP 201 Created。</summary>
    private const int HttpStatus201Created = 201;

    /// <summary>HTTP 204 No Content。</summary>
    private const int HttpStatus204NoContent = 204;

    /// <summary>
    /// 実行を開始する。HTTP 受理時は work queue へ enqueue、Worker / 同期経路は同一プロセスで Engine を起動する。
    /// </summary>
    /// <param name="request">開始リクエスト。</param>
    /// <param name="idempotencyKey">冪等キー（任意）。</param>
    /// <param name="requestContext">HTTP / Worker 文脈（Method / Path）。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>表示 ID 付きの実行応答。</returns>
    /// <exception cref="NotFoundException">定義またはバージョンが見つからないとき。</exception>
    public async Task<ExecutionResponse> StartAsync(
        StartExecutionRequest request,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantKey = tenantContext.GetRequiredTenantKey();
        var requestHash = ComputeStartRequestHash(request);
        var dedupKey = idempotency.CreateKey(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path, requestHash);

        var cachedStart = await idempotency.TryGetIdempotentStartResponseAsync(dedupKey, requestHash, ct).ConfigureAwait(false);
        if (cachedStart is not null)
        {
            return cachedStart;
        }

        var defUuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Definition, request.DefinitionId!, ct).ConfigureAwait(false);
        if (defUuid is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        var tenantId = tenantContext.GetRequiredTenantId();
        var versionRow = await ResolveStartDefinitionVersionAsync(tenantId, defUuid.Value, request, ct).ConfigureAwait(false);
        if (versionRow is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        await EnsureCanExecuteOnDefinitionAsync(tenantId, defUuid.Value, ct).ConfigureAwait(false);

        // HTTP 受理経路: キューがある場合は Engine を回さず Start work item を投入する。
        // Worker 文脈（またはテストでキュー未注入）は従来どおり同一プロセスで実行する。
        if (workQueue is not null
            && !string.Equals(requestContext.Method, "WORKER", StringComparison.Ordinal))
        {
            return await AcceptStartAndEnqueueAsync(
                    request,
                    requestHash,
                    dedupKey,
                    tenantId,
                    defUuid.Value,
                    versionRow,
                    ct)
                .ConfigureAwait(false);
        }

        return await ExecuteStartInProcessAsync(
                new ExecuteStartInProcessArgs(
                    request,
                    requestHash,
                    dedupKey,
                    tenantId,
                    defUuid.Value,
                    versionRow,
                    ExecutionId: null),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 実行行を受理し <see cref="ExecutionWorkItemKinds.Start"/> を enqueue する（Engine は回さない）。
    /// </summary>
    /// <param name="request">開始リクエスト。</param>
    /// <param name="requestHash">冪等用リクエストハッシュ。</param>
    /// <param name="dedupKey">command_dedup キー（任意）。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 UUID。</param>
    /// <param name="versionRow">採用する定義バージョン。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>受理直後の実行応答（Running）。</returns>
    private async Task<ExecutionResponse> AcceptStartAndEnqueueAsync(
        StartExecutionRequest request,
        string requestHash,
        CommandDedupKey? dedupKey,
        Guid tenantId,
        Guid definitionId,
        DefinitionVersionRow versionRow,
        CancellationToken ct)
    {
        var executionId = idGenerator.NewSequentialGuid();
        var graphId = await displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, definitionId.ToString("D"), ct).ConfigureAwait(false)
            ?? definitionId.ToString("D");
        const string emptyGraphJson = """{"nodes":[],"edges":[]}""";

        var response = await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                var createdAt = DateTime.UtcNow;
                var securitySnapshot = await snapshotFactory
                    .CaptureForStartAsync(tenantId, definitionId, createdAt, innerCt)
                    .ConfigureAwait(false);
                var securitySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(securitySnapshot);
                var displayId = await displayIdWrites
                    .AllocateAsync(uow, DisplayIdResourceTypes.Execution, executionId, innerCt)
                    .ConfigureAwait(false);

                var executionRow = new ExecutionRow
                {
                    ExecutionId = executionId,
                    TenantId = tenantId,
                    DefinitionId = definitionId,
                    DefinitionVersionId = versionRow.DefinitionVersionId,
                    Status = ExecutionProjectionStatuses.Running,
                    StartedAt = createdAt,
                    UpdatedAt = createdAt,
                    CancelRequested = false,
                    RestartLost = false,
                    SecuritySnapshotJson = securitySnapshotJson
                };
                var snapshotRow = new ExecutionGraphSnapshotRow
                {
                    ExecutionId = executionId,
                    GraphJson = emptyGraphJson,
                    UpdatedAt = createdAt
                };
                var accepted = new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = executionId,
                    GraphId = graphId,
                    Status = ExecutionProjectionStatuses.Running,
                    StartedAt = createdAt
                };

                await executions.AddExecutionAndSnapshotAsync(uow, executionRow, snapshotRow, innerCt).ConfigureAwait(false);

                var startedPayload = JsonSerializer.Serialize(
                    new
                    {
                        definitionId = definitionId.ToString(),
                        definitionVersionId = versionRow.DefinitionVersionId.ToString(),
                        definitionVersion = versionRow.Version,
                        tenantId,
                        startedByPrincipalId = securitySnapshot.StartedByPrincipalId,
                        permissionSetHash = securitySnapshot.PermissionSetHash,
                        securityModelVersion = securitySnapshot.SecurityModelVersion,
                        evaluationMode = securitySnapshot.EvaluationMode.ToString()
                    },
                    JsonSerializerProfiles.CamelCase);
                await eventStore
                    .AppendAsync(uow, executionId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                    .ConfigureAwait(false);

                if (dedupKey is { } saveKey)
                {
                    var responseJson = JsonSerializer.Serialize(accepted, JsonSerializerProfiles.CamelCase);
                    await dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = requestHash,
                            StatusCode = HttpStatus201Created,
                            ResponseBody = responseJson,
                            CreatedAt = createdAt,
                            ExpiresAt = createdAt.AddHours(24)
                        },
                        innerCt).ConfigureAwait(false);
                }

                await workQueue!.EnqueueAsync(
                    uow,
                    new ExecutionWorkItemRow
                    {
                        WorkItemId = idGenerator.NewSequentialGuid(),
                        ExecutionId = executionId,
                        Kind = ExecutionWorkItemKinds.Start,
                        Payload = JsonSerializer.Serialize(
                            new ExecutionStartWorkItemPayload(executionId, request),
                            ExecutionWorkItemPayloadJson.Options),
                        AvailableAt = createdAt,
                        Attempts = 0,
                        CreatedAt = createdAt
                    },
                    innerCt).ConfigureAwait(false);

                return accepted;
            },
            ct).ConfigureAwait(false);

        return response;
    }

    /// <summary>
    /// 同一プロセス内で Engine を起動し投影する（Worker / テスト互換）。
    /// </summary>
    /// <remarks>
    /// <para><paramref name="args"/>.ExecutionId がある場合は受理済み行の QueuedStart。無い場合は新規 Start。</para>
    /// <para>同期経路で durable Wait があるときは checkpoint 保存後に Unload する。</para>
    /// </remarks>
    /// <param name="args">Start 入力束。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>投影反映後の実行応答。</returns>
    private async Task<ExecutionResponse> ExecuteStartInProcessAsync(
        ExecuteStartInProcessArgs args,
        CancellationToken ct)
    {
        var compiled = RestoreCompiledDefinitionFromVersion(args.VersionRow);
        var resolvedExecutionId = args.ExecutionId ?? idGenerator.NewSequentialGuid();
        var engineId = resolvedExecutionId.ToString();
        engine.Start(compiled, engineId, args.Request.Input, args.Request.InitialState);

        var graphId = await displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Definition, args.DefinitionId.ToString("D"), ct).ConfigureAwait(false)
            ?? args.DefinitionId.ToString("D");

        try
        {
            var startResult = await executor.ExecuteReadCommittedAsync(
                async (uow, innerCt) =>
                {
                    var createdAt = DateTime.UtcNow;
                    var startSnapshot = engine.GetSnapshot(engineId)
                        ?? throw new InvalidOperationException(
                            $"Cannot project execution '{resolvedExecutionId}': engine snapshot is missing after Start.");
                    var status = ExecutionProjectionOrchestrator.MapStatus(startSnapshot);
                    var graphJson = engine.ExportExecutionGraph(engineId);

                    ExecutionResponse response;
                    if (args.ExecutionId is null)
                    {
                        // 同一プロセス受理: セキュリティ snapshot と WorkflowStarted をここで確定する。
                        var securitySnapshot = await snapshotFactory
                            .CaptureForStartAsync(args.TenantId, args.DefinitionId, createdAt, innerCt)
                            .ConfigureAwait(false);
                        var securitySnapshotJson = ExecutionSecuritySnapshotJson.Serialize(securitySnapshot);

                        var startedPayload = JsonSerializer.Serialize(
                            new
                            {
                                definitionId = args.DefinitionId.ToString(),
                                definitionVersionId = args.VersionRow.DefinitionVersionId.ToString(),
                                definitionVersion = args.VersionRow.Version,
                                tenantId = args.TenantId,
                                startedByPrincipalId = securitySnapshot.StartedByPrincipalId,
                                permissionSetHash = securitySnapshot.PermissionSetHash,
                                securityModelVersion = securitySnapshot.SecurityModelVersion,
                                evaluationMode = securitySnapshot.EvaluationMode.ToString()
                            },
                            JsonSerializerProfiles.CamelCase);

                        var displayId = await displayIdWrites
                            .AllocateAsync(uow, DisplayIdResourceTypes.Execution, resolvedExecutionId, innerCt)
                            .ConfigureAwait(false);

                        var executionRow = new ExecutionRow
                        {
                            ExecutionId = resolvedExecutionId,
                            TenantId = args.TenantId,
                            DefinitionId = args.DefinitionId,
                            DefinitionVersionId = args.VersionRow.DefinitionVersionId,
                            Status = status,
                            StartedAt = createdAt,
                            UpdatedAt = createdAt,
                            CancelRequested = false,
                            RestartLost = false,
                            SecuritySnapshotJson = securitySnapshotJson
                        };
                        var snapshotRow = new ExecutionGraphSnapshotRow
                        {
                            ExecutionId = resolvedExecutionId,
                            GraphJson = graphJson,
                            UpdatedAt = createdAt
                        };
                        response = new ExecutionResponse
                        {
                            DisplayId = displayId,
                            ResourceId = resolvedExecutionId,
                            GraphId = graphId,
                            Status = status,
                            StartedAt = createdAt
                        };

                        await executions.AddExecutionAndSnapshotAsync(uow, executionRow, snapshotRow, innerCt).ConfigureAwait(false);
                        await eventStore
                            .AppendAsync(uow, resolvedExecutionId, EventStoreEventType.WorkflowStarted, startedPayload, innerCt)
                            .ConfigureAwait(false);

                        if (args.DedupKey is { } saveKey)
                        {
                            var responseJson = JsonSerializer.Serialize(response, JsonSerializerProfiles.CamelCase);
                            await dedup.SaveAsync(
                                uow,
                                new CommandDedupRow
                                {
                                    DedupKey = saveKey.DedupKey,
                                    Endpoint = saveKey.Endpoint,
                                    IdempotencyKey = saveKey.IdempotencyKey,
                                    RequestHash = args.RequestHash,
                                    StatusCode = HttpStatus201Created,
                                    ResponseBody = responseJson,
                                    CreatedAt = createdAt,
                                    ExpiresAt = createdAt.AddHours(24)
                                },
                                innerCt).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        // Worker: HTTP 受理時に snapshot / WorkflowStarted 済み。投影のみ更新する。
                        var existing = await executions.GetByIdAsync(uow, args.TenantId, resolvedExecutionId, innerCt).ConfigureAwait(false)
                            ?? throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
                        var displayId = await displayIds.GetDisplayIdAsync(
                                DisplayIdResourceTypes.Execution,
                                resolvedExecutionId.ToString("D"),
                                innerCt)
                            .ConfigureAwait(false)
                            ?? resolvedExecutionId.ToString("D");
                        response = new ExecutionResponse
                        {
                            DisplayId = displayId,
                            ResourceId = resolvedExecutionId,
                            GraphId = graphId,
                            Status = status,
                            StartedAt = existing.StartedAt
                        };
                        await executions
                            .UpdateExecutionAndSnapshotAsync(
                                uow,
                                resolvedExecutionId,
                                status,
                                cancelRequested: existing.CancelRequested,
                                graphJson,
                                innerCt)
                            .ConfigureAwait(false);
                    }

                    await projection.SyncOperationalProjectionAsync(
                            uow,
                            resolvedExecutionId,
                            args.TenantId,
                            status,
                            graphJson,
                            nodeIdToClear: null,
                            innerCt)
                        .ConfigureAwait(false);

                    var checkpointSaved = false;
                    if (string.Equals(status, ExecutionProjectionStatuses.Running, StringComparison.Ordinal)
                        && ExecutionOperationalProjectionSync.HasDurableWaits(graphJson))
                    {
                        checkpointSaved = await UpsertRuntimeCheckpointAsync(uow, engineId, resolvedExecutionId, innerCt)
                            .ConfigureAwait(false);
                    }

                    return (Response: response, UnloadAfterCommit: checkpointSaved);
                },
                ct).ConfigureAwait(false);

            if (startResult.UnloadAfterCommit)
            {
                engine.Unload(engineId);
                logger.CheckpointUnloadedAfterStartSync(resolvedExecutionId);
            }

            return startResult.Response;
        }
        catch
        {
            // DB 反映前に失敗した場合、再試行で二重 Load しないようローカル Engine を捨てる。
            engine.Unload(engineId);
            throw;
        }
    }

    /// <summary>
    /// 受理済み実行の Start work item を同一プロセスで実行する（Worker 用）。
    /// </summary>
    /// <param name="executionId">受理済み実行 ID。</param>
    /// <param name="request">開始時の入力（定義解決済み行と整合）。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>投影反映後の実行応答。</returns>
    /// <exception cref="NotFoundException">実行または定義バージョンが見つからないとき。</exception>
    public async Task<ExecutionResponse> ExecuteQueuedStartAsync(
        Guid executionId,
        StartExecutionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureExecutionsWriteAsync(ct).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var versionRow = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => definitions.GetVersionForExecutionByIdAsync(
                uow,
                tenantId,
                execution.DefinitionVersionId,
                innerCt),
            ct).ConfigureAwait(false);
        if (versionRow is null)
            throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

        return await ExecuteStartInProcessAsync(
                new ExecuteStartInProcessArgs(
                    request,
                    RequestHash: string.Empty,
                    DedupKey: null,
                    tenantId,
                    execution.DefinitionId,
                    versionRow,
                    executionId),
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Engine から runtime checkpoint を export し永続化する。
    /// </summary>
    /// <remarks>
    /// Worker 所有中は世代付き upsert。所有外は通常 upsert。export が null のときは false。
    /// </remarks>
    /// <param name="uow">単位作業（UoW）。</param>
    /// <param name="engineExecutionId">Engine 上の実行 ID 文字列。</param>
    /// <param name="executionId">永続化側の実行 UUID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>保存できたとき true。export 失敗または世代衝突時は false。</returns>
    public async Task<bool> UpsertRuntimeCheckpointAsync(
        ICoreUnitOfWork uow,
        string engineExecutionId,
        Guid executionId,
        CancellationToken ct)
    {
        var checkpoint = engine.ExportCheckpoint(engineExecutionId);
        if (checkpoint is null)
        {
            logger.ExportCheckpointNullWithDurableWaits(engineExecutionId);
            return false;
        }

        var json = JsonSerializer.Serialize(checkpoint, JsonSerializerProfiles.CamelCase);
        var updatedAt = DateTime.UtcNow;
        if (ownership.TryGet(executionId, out _, out var generation))
        {
            return await checkpointStore.TryUpsertRuntimeWithGenerationAsync(
                    uow,
                    new ExecutionCheckpointRuntimeUpsert(
                        executionId,
                        generation,
                        json,
                        checkpoint.SchemaVersion,
                        updatedAt),
                    ct)
                .ConfigureAwait(false);
        }

        await checkpointStore.UpsertAsync(
            uow,
            new ExecutionCheckpointDocument
            {
                ExecutionId = executionId,
                CheckpointJson = json,
                SchemaVersion = checkpoint.SchemaVersion,
                UpdatedAt = updatedAt
            },
            ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 実行をキャンセルする。HTTP 受理時は work queue、Worker / 同期経路は Engine Cancel と投影更新を行う。
    /// </summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="idempotencyKey">冪等キー（任意）。</param>
    /// <param name="requestContext">HTTP / Worker 文脈。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">実行が見つからないとき。</exception>
    public async Task CancelAsync(
        string idOrUuid,
        string? idempotencyKey,
        CommandRequestContext requestContext,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var tenantKey = tenantContext.GetRequiredTenantKey();
        var tenantId = tenantContext.GetRequiredTenantId();
        var dedupKey = idempotency.CreateKey(tenantKey, idempotencyKey, requestContext.Method, requestContext.Path);

        if (await idempotency.HasValidCommandDedupAsync(dedupKey, ct).ConfigureAwait(false))
            return;

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        await EnsureExecutionMutationWriteAsync(execution, ct).ConfigureAwait(false);

        // HTTP 受理経路: キューがある場合は Engine を回さず Cancel work item を投入する。
        if (workQueue is not null
            && !string.Equals(requestContext.Method, "WORKER", StringComparison.Ordinal))
        {
            await AcceptCancelAndEnqueueAsync(uuid.Value, dedupKey, ct).ConfigureAwait(false);
            return;
        }

        // WORKER / 同期 Cancel: 未終端子へ Cancel をカスケード（ネストは各層で再帰）。
        if (forkChildCoordinator is not null)
        {
            await forkChildCoordinator
                .CascadeCancelToRunningChildrenAsync(uuid.Value, ct)
                .ConfigureAwait(false);
        }

        await projection.DrainAsync(uuid.Value, ct).ConfigureAwait(false);

        var clientEventId = ClientEventIdResolver.FromIdempotencyKey(idempotencyKey, idGenerator);

        if (await idempotency.TryBeginEventDeliveryOrAbortIfAlreadyAppliedAsync(uuid.Value, clientEventId, ct).ConfigureAwait(false))
            return;

        await EnsureEngineRuntimeLoadedForMutationAsync(uuid.Value, execution, ct).ConfigureAwait(false);

        var cancelApply = await engine.CancelAsync(uuid.Value.ToString(), clientEventId).ConfigureAwait(false);
        var skipCancelEventAppend = cancelApply.IsAlreadyApplied;

        var (projStatus, projCancel, projGraphJson) = projection.BuildProjectionFromEngine(uuid.Value);
        var cancelPayload = JsonSerializer.Serialize(
            new { tenantId },
            JsonSerializerProfiles.CamelCase);

        await mutationPersistence.ExecuteSerializableWithRetryAsync(
            tenantId,
            uuid.Value,
            clientEventId,
            async (uow, ctInner) =>
            {
                await executions
                    .UpdateExecutionAndSnapshotAsync(uow, uuid.Value, projStatus, projCancel, projGraphJson, ctInner)
                    .ConfigureAwait(false);
                await projection.SyncOperationalProjectionAsync(
                        uow,
                        uuid.Value,
                        tenantId,
                        projStatus,
                        projGraphJson,
                        nodeIdToClear: null,
                        ctInner)
                    .ConfigureAwait(false);
                if (!skipCancelEventAppend)
                {
                    await eventStore
                        .AppendAsync(uow, uuid.Value, EventStoreEventType.WorkflowCancelled, cancelPayload, ctInner)
                        .ConfigureAwait(false);
                }
                else
                {
                    await eventStore.TryAppendIfAbsentByClientEventAsync(
                        uow,
                        uuid.Value,
                        clientEventId,
                        EventStoreEventType.WorkflowCancelled,
                        cancelPayload,
                        ctInner).ConfigureAwait(false);
                }

                if (dedupKey is { } saveKey)
                {
                    var now = DateTime.UtcNow;
                    await dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = HttpStatus204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        ctInner).ConfigureAwait(false);
                }

                var nowUtc = DateTime.UtcNow;
                await eventDeliveryDedup.TryUpdateStatusAsync(
                    uow,
                    tenantId,
                    uuid.Value,
                    clientEventId,
                    new EventDeliveryDedupStatusUpdate(
                        EventDeliveryDedupStatuses.Applied,
                        nowUtc,
                        AppliedAt: nowUtc,
                        ErrorCode: null),
                    ctInner).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel work item を enqueue し、任意で command_dedup を保存する。
    /// </summary>
    /// <param name="executionId">対象実行 ID。</param>
    /// <param name="dedupKey">command_dedup キー（任意）。</param>
    /// <param name="ct">キャンセル。</param>
    private async Task AcceptCancelAndEnqueueAsync(
        Guid executionId,
        CommandDedupKey? dedupKey,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await executor.ExecuteReadCommittedAsync(
            async (uow, innerCt) =>
            {
                if (dedupKey is { } saveKey)
                {
                    await dedup.SaveAsync(
                        uow,
                        new CommandDedupRow
                        {
                            DedupKey = saveKey.DedupKey,
                            Endpoint = saveKey.Endpoint,
                            IdempotencyKey = saveKey.IdempotencyKey,
                            RequestHash = null,
                            StatusCode = HttpStatus204NoContent,
                            ResponseBody = null,
                            CreatedAt = now,
                            ExpiresAt = now.AddHours(24)
                        },
                        innerCt).ConfigureAwait(false);
                }

                await workQueue!.EnqueueAsync(
                    uow,
                    new ExecutionWorkItemRow
                    {
                        WorkItemId = idGenerator.NewSequentialGuid(),
                        ExecutionId = executionId,
                        Kind = ExecutionWorkItemKinds.Cancel,
                        Payload = "{}",
                        AvailableAt = now,
                        Attempts = 0,
                        CreatedAt = now
                    },
                    innerCt).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>Start 用の定義バージョン行を解決する（versionId / version 番号 / latest）。</summary>
    private Task<DefinitionVersionRow?> ResolveStartDefinitionVersionAsync(
        Guid tenantId,
        Guid definitionId,
        StartExecutionRequest request,
        CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => ResolveStartDefinitionVersionInUowAsync(uow, tenantId, definitionId, request, innerCt),
            ct);

    /// <summary>UoW 内で Start 用定義バージョンを解決する。</summary>
    private async Task<DefinitionVersionRow?> ResolveStartDefinitionVersionInUowAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        StartExecutionRequest request,
        CancellationToken ct)
    {
        if (request.DefinitionVersionId is { } versionId)
            return await ResolveVersionByIdForAdmissionAsync(uow, tenantId, definitionId, versionId, ct)
                .ConfigureAwait(false);

        if (request.DefinitionVersion is { } versionNumber)
            return await ResolveVersionByNumberForAdmissionAsync(uow, tenantId, definitionId, versionNumber, ct)
                .ConfigureAwait(false);

        var latest = await definitions
            .GetLatestForApiAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        return latest?.Version;
    }

    /// <summary>versionId 指定の受理。親定義が Active であることと定義所属を検証する。</summary>
    private async Task<DefinitionVersionRow?> ResolveVersionByIdForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        Guid versionId,
        CancellationToken ct)
    {
        var byId = await definitions
            .GetVersionForExecutionByIdAsync(uow, tenantId, versionId, ct)
            .ConfigureAwait(false);
        if (byId is null || byId.DefinitionId != definitionId)
            return null;

        return await EnsureActiveParentForAdmissionAsync(uow, tenantId, definitionId, ct).ConfigureAwait(false)
            ? byId
            : null;
    }

    /// <summary>version 番号指定の受理。親定義が Active であることを検証する。</summary>
    private async Task<DefinitionVersionRow?> ResolveVersionByNumberForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int versionNumber,
        CancellationToken ct)
    {
        var byNumber = await definitions
            .GetVersionForExecutionAsync(uow, tenantId, definitionId, versionNumber, ct)
            .ConfigureAwait(false);
        if (byNumber is null)
            return null;

        return await EnsureActiveParentForAdmissionAsync(uow, tenantId, definitionId, ct).ConfigureAwait(false)
            ? byNumber
            : null;
    }

    /// <summary>親定義が API 向け Active（latest）として存在するか。</summary>
    private async Task<bool> EnsureActiveParentForAdmissionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct)
    {
        var active = await definitions
            .GetLatestForApiAsync(uow, tenantId, definitionId, ct)
            .ConfigureAwait(false);
        return active is not null;
    }

    /// <summary>定義が属するプロジェクトで Execute 権限を要求する。</summary>
    private Task EnsureCanExecuteOnDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);

    /// <summary>
    /// 永続化された定義バージョンからコンパイル済み定義を復元する。
    /// </summary>
    /// <param name="versionRow">定義バージョン行。</param>
    /// <returns>コンパイル済みワークフロー定義。</returns>
    /// <exception cref="InvalidOperationException">格納 YAML / JSON が不正なとき。</exception>
    public CompiledWorkflowDefinition RestoreCompiledDefinitionFromVersion(DefinitionVersionRow versionRow)
    {
        try
        {
            return compiler.RestoreFromStoredVersion(versionRow.SourceYaml, versionRow.CompiledJson);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Stored definition version is invalid.", ex);
        }
    }

    /// <summary>Start リクエスト本文の正規化 SHA-256（hex）を返す。</summary>
    private static string ComputeStartRequestHash(StartExecutionRequest request)
    {
        var normalized = JsonSerializer.Serialize(
            new
            {
                definitionId = request.DefinitionId,
                definitionVersion = request.DefinitionVersion,
                definitionVersionId = request.DefinitionVersionId,
                input = request.Input
            },
            JsonSerializerProfiles.CamelCase);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>投影ステータスが終端（Succeeded / Failed / Cancelled 等）かどうか。</summary>
    /// <param name="status">投影 status 文字列。</param>
    /// <returns>終端なら true。</returns>
    internal static bool IsTerminalExecutionProjectionStatus(string status) =>
        ExecutionProjectionStatuses.IsTerminal(status);

    /// <summary>Runtime の executions:write を要求する。</summary>
    private Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    /// <summary>実行行の security snapshot に基づきミューテーション権限を要求する。</summary>
    private Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }

    /// <summary>
    /// ミューテーション前に Engine インスタンスを保証する（未ロードなら checkpoint から hydrate）。
    /// </summary>
    /// <remarks>
    /// 終端投影かつメモリ未ロード、または checkpoint 欠落・不正時は例外。二重 hydrate は 422 相当へ写像する。
    /// </remarks>
    /// <param name="executionId">実行 UUID。</param>
    /// <param name="execution">投影行（テナント・定義バージョン・status）。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="ArgumentException">終端未ロード、checkpoint 欠落、または二重 hydrate。</exception>
    /// <exception cref="InvalidOperationException">定義バージョン欠落または checkpoint 不正。</exception>
    public async Task EnsureEngineRuntimeLoadedForMutationAsync(
        Guid executionId,
        ExecutionRow execution,
        CancellationToken ct)
    {
        if (engine.GetSnapshot(executionId.ToString()) is not null)
            return;

        if (IsTerminalExecutionProjectionStatus(execution.Status))
        {
            throw new ArgumentException(
                "The execution is already in a terminal state in the database projection, but there is no in-memory instance in this API process. Cancel or event delivery cannot be applied.");
        }

        var checkpointDocument = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => checkpointStore.GetByExecutionIdAsync(uow, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (checkpointDocument is null)
        {
            throw new ArgumentException(
                "The execution state is not loaded in this API process and no runtime checkpoint was found.");
        }

        var versionRow = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => definitions.GetVersionForExecutionByIdAsync(
                uow,
                execution.TenantId,
                execution.DefinitionVersionId,
                innerCt),
            ct).ConfigureAwait(false);
        if (versionRow is null)
        {
            throw new InvalidOperationException(
                $"Definition version '{execution.DefinitionVersionId}' was not found for execution '{executionId}'.");
        }

        var compiled = RestoreCompiledDefinitionFromVersion(versionRow);
        if (!TryParseRuntimeCheckpoint(checkpointDocument.CheckpointJson, out var checkpoint, out var parseError))
        {
            logger.RuntimeCheckpointInvalidForHydrate(
                executionId,
                checkpointDocument.CheckpointJson?.Length ?? 0);
            if (parseError is not null)
                logger.RuntimeCheckpointDeserializeFailed(parseError, executionId);

            throw new InvalidOperationException(
                $"Stored runtime checkpoint for execution '{executionId:D}' is empty or invalid and cannot be hydrated.");
        }

        try
        {
            engine.ImportCheckpoint(compiled, checkpoint);
        }
        catch (InvalidOperationException ex)
        {
            // 二重 hydrate 等はクライアント向け 422（ArgumentException）へ写像する。
            throw new ArgumentException(ex.Message, ex);
        }
    }

    /// <summary>
    /// 永続 checkpoint JSON を hydrate 用に検証・デシリアライズする。
    /// </summary>
    /// <remarks>
    /// <c>BeginOwnedSessionAsync</c> の seed <c>{}</c> や必須欠落は不正とし、Import しない。
    /// </remarks>
    /// <param name="checkpointJson">永続化された checkpoint JSON。</param>
    /// <param name="checkpoint">成功時のデシリアライズ結果。</param>
    /// <param name="parseError">JSON 例外時のみ設定。形式不正（必須欠落）は null。</param>
    /// <returns>hydrate 可能なとき true。</returns>
    internal static bool TryParseRuntimeCheckpoint(
        string? checkpointJson,
        out ExecutionRuntimeCheckpoint checkpoint,
        out Exception? parseError)
    {
        checkpoint = null!;
        parseError = null;

        if (string.IsNullOrWhiteSpace(checkpointJson))
            return false;

        var trimmed = checkpointJson.Trim();
        if (trimmed is "{}" or "null")
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<ExecutionRuntimeCheckpoint>(
                checkpointJson,
                JsonSerializerProfiles.CamelCase);
            if (parsed is null
                || string.IsNullOrWhiteSpace(parsed.ExecutionId)
                || string.IsNullOrWhiteSpace(parsed.DefinitionName)
                || parsed.ActiveStates is null
                || parsed.Graph is null
                || parsed.Join is null
                || parsed.Context is null
                || parsed.PendingWaits is null)
            {
                return false;
            }

            // 物理 Join 待ち中は ActiveStates / PendingWaits が空の正規断面になり得る（Fork Unload 後）。
            // 空配列だけでは破損とみなさない（seed "{}" は上で拒否済み）。

            checkpoint = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            parseError = ex;
            return false;
        }
    }

    /// <summary>同一プロセス Start の入力束。</summary>
    private sealed record ExecuteStartInProcessArgs(
        StartExecutionRequest Request,
        string RequestHash,
        CommandDedupKey? DedupKey,
        Guid TenantId,
        Guid DefinitionId,
        DefinitionVersionRow VersionRow,
        Guid? ExecutionId);
}
