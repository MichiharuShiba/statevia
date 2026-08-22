namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行の read-model（一覧・単体・graph・waits・view・events）取得。
/// </summary>
/// <remarks>
/// <para><see cref="ExecutionService"/> Facade から委譲される。HTTP 契約は変更しない。</para>
/// <para>Hosted Fork の親 graph / イベント合成は <see cref="IForkChildExecutionCoordinator"/> 経由。</para>
/// </remarks>
/// <param name="displayIds">表示 ID 解決。</param>
/// <param name="executions">executions / snapshot 永続化。</param>
/// <param name="authorization">横断認可（read）。</param>
/// <param name="tenantContext">テナント文脈。</param>
/// <param name="eventStore">event_store 読み取り。</param>
/// <param name="executor">読み取りトランザクション実行。</param>
/// <param name="forkChildCoordinator">Fork 合成（未登録時は生 snapshot）。</param>
internal sealed class ExecutionQueryService(
    IDisplayIdService displayIds,
    IExecutionRepository executions,
    ExecutionAuthorizationGuard authorization,
    ITenantContextAccessor tenantContext,
    IEventStoreRepository eventStore,
    ICoreTransactionExecutor executor,
    IForkChildExecutionCoordinator? forkChildCoordinator = null)
{
    /// <summary>ページング一覧を返す。</summary>
    /// <param name="query">一覧クエリ。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>ページ結果。</returns>
    public async Task<PagedResult<ExecutionResponse>> ListPagedAsync(
        ExecutionListPageQuery query,
        CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        ArgumentNullException.ThrowIfNull(query);

        var (total, pairs) = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.ListWithDisplayIdsPageAsync(uow, tenantId, query, innerCt),
            ct).ConfigureAwait(false);
        var items = pairs.Select(p => new ExecutionResponse
        {
            DisplayId = p.DisplayId ?? p.Execution.ExecutionId.ToString(),
            ResourceId = p.Execution.ExecutionId,
            GraphId = p.Execution.DefinitionId.ToString("D"),
            Status = p.Execution.Status,
            StartedAt = p.Execution.StartedAt,
            UpdatedAt = p.Execution.UpdatedAt,
            CancelRequested = p.Execution.CancelRequested,
            RestartLost = p.Execution.RestartLost
        }).ToList();

        return new PagedResult<ExecutionResponse>
        {
            Items = items,
            TotalCount = total,
            Offset = query.Page.Offset,
            Limit = query.Page.Limit,
            HasMore = query.Page.Offset + items.Count < total
        };
    }

    /// <summary>単一取得（一覧と同形）。</summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>実行応答。</returns>
    public async Task<ExecutionResponse> GetExecutionResponseAsync(string idOrUuid, CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var execution = await executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, innerCt)
                    .ConfigureAwait(false) ?? execution.ExecutionId.ToString("D");
                var graphId = await displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, execution.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? execution.DefinitionId.ToString("D");

                return new ExecutionResponse
                {
                    DisplayId = displayId,
                    ResourceId = execution.ExecutionId,
                    GraphId = graphId,
                    Status = execution.Status,
                    StartedAt = execution.StartedAt,
                    UpdatedAt = execution.UpdatedAt,
                    CancelRequested = execution.CancelRequested,
                    RestartLost = execution.RestartLost
                };
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>投影済み実行グラフ JSON を返す（Fork 合成込み）。</summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>グラフ JSON。</returns>
    public async Task<string> GetGraphJsonAsync(string idOrUuid, CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
        await EnsureExecutionExistsAsync(uuid.Value, ct).ConfigureAwait(false);
        var row = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetSnapshotByExecutionIdAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (row is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        if (forkChildCoordinator is null)
            return row.GraphJson;

        return await forkChildCoordinator
            .ComposeReadModelGraphJsonAsync(uuid.Value, row.GraphJson, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 未完了 Wait の再開キー一覧を返す。正本は graph スナップショット（Hosted 親は GET 時合成後）。
    /// </summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>Wait 一覧。0 件でも空配列。</returns>
    /// <exception cref="NotFoundException">実行または graph スナップショットが無いとき（<see cref="GetGraphJsonAsync"/> と同じ）。</exception>
    public async Task<ExecutionWaitsResponse> GetExecutionWaitsAsync(string idOrUuid, CancellationToken ct)
    {
        var graphJson = await GetGraphJsonAsync(idOrUuid, ct).ConfigureAwait(false);
        return ExecutionViewMapper.MapActiveWaits(graphJson);
    }

    /// <summary>指定 execution がテナントに存在することを検証する。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionExistsAsync(Guid executionId, CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var tenantId = tenantContext.GetRequiredTenantId();
                var execution = await executions.GetByIdAsync(uow, tenantId, executionId, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);
            },
            ct);

    /// <summary>スナップショット行からグラフ JSON を取得する。無ければ <see langword="null"/>。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>グラフ JSON または null。</returns>
    public Task<string?> TryGetSnapshotGraphJsonByExecutionIdAsync(Guid executionId, CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var row = await executions.GetSnapshotByExecutionIdAsync(uow, executionId, innerCt).ConfigureAwait(false);
                return row?.GraphJson;
            },
            ct);

    /// <summary>現在の実行ビューを返す。</summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>実行ビュー。</returns>
    public async Task<ExecutionViewDto> GetExecutionViewAsync(string idOrUuid, CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    /// <summary>指定シーケンス時点に近い実行ビューを返す（リプレイ近似）。</summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="atSeq">対象シーケンス。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>実行ビュー。</returns>
    public async Task<ExecutionViewDto> GetExecutionViewAtSeqAsync(string idOrUuid, long atSeq, CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var maxSeq = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => eventStore.GetMaxSeqAsync(uow, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (maxSeq == 0)
            return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);

        if (atSeq > maxSeq)
            throw new NotFoundException("atSeq out of range");

        return await BuildExecutionViewInternalAsync(uuid.Value, idOrUuid, ct).ConfigureAwait(false);
    }

    /// <summary>event_store 由来のタイムラインイベントを返す。</summary>
    /// <param name="idOrUuid">表示 ID または UUID。</param>
    /// <param name="afterSeq">この seq より後を返す。</param>
    /// <param name="limit">最大件数。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>イベント一覧。</returns>
    public async Task<ExecutionEventsResponseDto> ListEventsAsync(
        string idOrUuid,
        long afterSeq,
        int limit,
        CancellationToken ct)
    {
        await authorization.EnsureExecutionsReadAsync(ct).ConfigureAwait(false);

        var tenantId = tenantContext.GetRequiredTenantId();
        var uuid = await displayIds.ResolveAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var execution = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => executions.GetByIdAsync(uow, tenantId, uuid.Value, innerCt),
            ct).ConfigureAwait(false);
        if (execution is null)
            throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

        var displayId = await displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false)
            ?? execution.ExecutionId.ToString("D");
        // D6 / D6.1: GraphUpdated patch は合成グラフ由来。
        var graphJson = await GetGraphJsonAsync(idOrUuid, ct).ConfigureAwait(false);
        var patchNodes = ExecutionViewMapper.MapGraphPatchNodes(graphJson);

        var sourceRows = await LoadEventStoreRowsForTimelineAsync(uuid.Value, ct).ConfigureAwait(false);
        var (events, hasMore) = PhysicalForkEventTimelineComposer.ComposePage(
            sourceRows,
            displayId,
            patchNodes,
            afterSeq,
            limit);

        return new ExecutionEventsResponseDto { Events = events, HasMore = hasMore };
    }

    /// <summary>
    /// 親＋再帰子孫の event_store 行を読み取り合成用に集める（D6.1）。
    /// </summary>
    private async Task<IReadOnlyList<PhysicalForkEventTimelineComposer.SourceRow>> LoadEventStoreRowsForTimelineAsync(
        Guid rootExecutionId,
        CancellationToken ct)
    {
        var executionIds = new List<Guid> { rootExecutionId };
        if (forkChildCoordinator is not null)
        {
            var descendants = await forkChildCoordinator
                .ListDescendantExecutionIdsAsync(rootExecutionId, ct)
                .ConfigureAwait(false);
            executionIds.AddRange(descendants);
        }

        var rows = new List<PhysicalForkEventTimelineComposer.SourceRow>();
        foreach (var executionId in executionIds)
        {
            var isRoot = executionId == rootExecutionId;
            long pageAfterSeq = 0;
            while (true)
            {
                var pageAfter = pageAfterSeq;
                var (items, hasMore) = await executor.ExecuteReadOnlyAsync(
                        (uow, innerCt) => eventStore.ListAfterSeqAsync(
                            uow,
                            executionId,
                            pageAfter,
                            limit: 500,
                            innerCt),
                        ct)
                    .ConfigureAwait(false);

                foreach (var item in items)
                    rows.Add(new PhysicalForkEventTimelineComposer.SourceRow(item, isRoot));

                if (!hasMore || items.Count == 0)
                    break;

                pageAfterSeq = items[^1].Seq;
                // 暴走防止（異常に長い event_store）。
                if (rows.Count >= 50_000)
                    break;
            }
        }

        return rows;
    }

    private async Task<ExecutionViewDto> BuildExecutionViewInternalAsync(Guid uuid, string idOrUuidForDisplay, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return await executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var execution = await executions.GetByIdAsync(uow, tenantId, uuid, innerCt).ConfigureAwait(false);
                if (execution is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var snapshot = await executions.GetSnapshotByExecutionIdAsync(uow, uuid, innerCt).ConfigureAwait(false);
                if (snapshot is null)
                    throw new NotFoundException(ExecutionValidationMessages.ExecutionNotFound);

                var displayId = await displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuidForDisplay, innerCt)
                    .ConfigureAwait(false) ?? execution.ExecutionId.ToString("D");
                var graphId = await displayIds
                    .GetDisplayIdAsync(DisplayIdResourceTypes.Definition, execution.DefinitionId.ToString("D"), innerCt)
                    .ConfigureAwait(false) ?? execution.DefinitionId.ToString("D");

                var graphJson = snapshot.GraphJson;
                if (forkChildCoordinator is not null)
                {
                    graphJson = await forkChildCoordinator
                        .ComposeReadModelGraphJsonAsync(uuid, snapshot.GraphJson, innerCt)
                        .ConfigureAwait(false);
                }

                return ExecutionViewMapper.BuildExecutionView(execution, graphJson, displayId, graphId);
            },
            ct).ConfigureAwait(false);
    }
}
