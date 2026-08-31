using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// execution_cursors / execution_waits を execution 投影更新と同一 tx で同期する。
/// cursor は operational projection。read-model（executions / execution_graph_snapshots）の正本ではない。
/// durable wait は Engine Wait ノード（EventWait）のみ永続化する。
/// </summary>
internal static class ExecutionOperationalProjectionSync
{
    /// <summary>
    /// 投影フラッシュと同一トランザクション内で cursor / durable wait を同期する。
    /// Publish / Resume 時は <see cref="ExecutionOperationalProjectionSyncRequest.NodeIdToClear"/> で該当 wait を先行削除する。
    /// </summary>
    public static async Task SyncAsync(
        ICoreUnitOfWork uow,
        IExecutionCursorRepository cursors,
        IExecutionWaitRepository waits,
        ExecutionOperationalProjectionSyncRequest request,
        IIdGenerator idGenerator,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeIdToClear))
        {
            await waits.DeleteByNodeIdAsync(uow, request.ExecutionId, request.NodeIdToClear, ct)
                .ConfigureAwait(false);
        }

        if (IsTerminalStatus(request.Status))
        {
            await cursors.DeleteAsync(uow, request.ExecutionId, ct).ConfigureAwait(false);
            await waits.ReplaceWaitsAsync(uow, request.ExecutionId, Array.Empty<ExecutionWaitRow>(), ct)
                .ConfigureAwait(false);
            return;
        }

        var now = DateTime.UtcNow;
        var activeNode = SelectActiveNode(request.GraphJson, request.Snapshot);
        await cursors.UpsertAsync(
            uow,
            new ExecutionCursorRow
            {
                ExecutionId = request.ExecutionId,
                TenantId = request.TenantId,
                CurrentNodeId = activeNode?.NodeId,
                CurrentRuntimeId = null,
                CurrentWorkerId = activeNode?.WorkerId,
                State = request.Status,
                UpdatedAt = now
            },
            ct).ConfigureAwait(false);

        // Resume / Publish 直後はグラフ完了反映が遅れることがある。
        // NodeIdToClear を除外しないと、削除した wait を古いグラフから即再作成してしまう。
        var durableWaits = ExtractDurableWaits(request.ExecutionId, request.GraphJson, now, idGenerator)
            .Where(row =>
                string.IsNullOrWhiteSpace(request.NodeIdToClear)
                || !string.Equals(row.NodeId, request.NodeIdToClear, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await waits.ReplaceWaitsAsync(uow, request.ExecutionId, durableWaits, ct).ConfigureAwait(false);
    }

    private static bool IsTerminalStatus(string status) =>
        ExecutionProjectionStatuses.IsTerminal(status);

    private static ActiveNodeSelection? SelectActiveNode(string graphJson, ExecutionSnapshot? snapshot)
    {
        if (!TryParseGraph(graphJson, out var nodes) || nodes.Count == 0)
            return null;

        var runningNodes = nodes
            .Where(n => n.CompletedAt is null && !string.IsNullOrWhiteSpace(n.NodeId))
            .ToList();
        if (runningNodes.Count == 0)
            return null;

        var waitCandidates = runningNodes
            .Where(n =>
                string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && HasConfiguredWaitEvents(n))
            .OrderByDescending(n => n.StartedAt)
            .ToList();

        if (waitCandidates.Count > 0)
        {
            var selected = waitCandidates[0];
            return new ActiveNodeSelection(selected.NodeId!, selected.WorkerId);
        }

        if (snapshot?.ActiveStates is { Count: > 0 })
        {
            var activeStateSet = snapshot.ActiveStates.ToHashSet(StringComparer.Ordinal);
            var activeStateNode = runningNodes
                .Where(n => !string.IsNullOrWhiteSpace(n.NodeName) && activeStateSet.Contains(n.NodeName))
                .OrderByDescending(n => n.StartedAt)
                .FirstOrDefault();
            if (activeStateNode is not null)
                return new ActiveNodeSelection(activeStateNode.NodeId!, activeStateNode.WorkerId);
        }

        var fallback = runningNodes.OrderByDescending(n => n.StartedAt).First();
        return new ActiveNodeSelection(fallback.NodeId!, fallback.WorkerId);
    }

    /// <summary>
    /// グラフ JSON に未完了の durable Wait（許可イベント付き）があるか。
    /// </summary>
    /// <param name="graphJson">実行グラフ JSON。</param>
    /// <returns>durable Wait が 1 件以上あれば <see langword="true"/>。</returns>
    internal static bool HasDurableWaits(string graphJson) =>
        ExtractDurableWaits(Guid.Empty, graphJson, DateTime.UtcNow, PresenceCheckIdGenerator.Instance).Count > 0;

    /// <summary>
    /// 未完了 Wait ノードから durable wait 行を構築する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 許可イベントはグラフの <c>allowedEvents</c>（WaitEventRouteTable 由来）を正本とする。
    /// 旧単一イベント互換として <c>waitKey</c> のみのノードも受け入れる。
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ExecutionWaitRow> ExtractDurableWaits(
        Guid executionId,
        string graphJson,
        DateTime nowUtc,
        IIdGenerator idGenerator)
    {
        if (!TryParseGraph(graphJson, out var nodes))
            return Array.Empty<ExecutionWaitRow>();

        return nodes
            .Where(n =>
                n.CompletedAt is null
                && !string.IsNullOrWhiteSpace(n.NodeId)
                && string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase))
            .Select(n =>
            {
                var allowedEvents = ResolveAllowedEvents(n);
                if (allowedEvents.Count == 0)
                    return null;

                return new ExecutionWaitRow
                {
                    ExecutionId = executionId,
                    NodeId = n.NodeId!,
                    WaitKind = ExecutionWaitKind.EventWait,
                    AllowedEvents = allowedEvents,
                    ExpiresAt = null,
                    CreatedAt = nowUtc,
                    Subscriptions = ExtractSubscriptions(executionId, n.NodeId!, n.Subscriptions, nowUtc, idGenerator)
                };
            })
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();
    }

    /// <summary>グラフの subscriptions を子テーブル行へ写像する。</summary>
    private static IReadOnlyList<ExecutionWaitSubscriptionRow> ExtractSubscriptions(
        Guid executionId,
        string nodeId,
        List<GraphSubscriptionDto>? subscriptions,
        DateTime nowUtc,
        IIdGenerator idGenerator)
    {
        if (subscriptions is null || subscriptions.Count == 0)
            return Array.Empty<ExecutionWaitSubscriptionRow>();

        return subscriptions
            .Where(s => !string.IsNullOrWhiteSpace(s.Topic) && !string.IsNullOrWhiteSpace(s.ResumeEventName))
            .Select(s => new ExecutionWaitSubscriptionRow
            {
                SubscriptionId = idGenerator.NewSequentialGuid(),
                ExecutionId = executionId,
                NodeId = nodeId,
                Topic = s.Topic!.Trim(),
                CorrelationKey = NormalizeKey(s.Key),
                ResumeEventName = s.ResumeEventName!.Trim(),
                CreatedAt = nowUtc
            })
            .ToList();
    }

    /// <summary>
    /// グラフノードから許可イベント一覧を解決する（allowedEvents 優先、なければ waitKey）。
    /// </summary>
    private static List<string> ResolveAllowedEvents(GraphNodeDto node)
    {
        if (node.AllowedEvents is { Count: > 0 })
        {
            return node.AllowedEvents
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(node.WaitKey))
            return [node.WaitKey.Trim()];

        return [];
    }

    /// <summary>
    /// durable wait / cursor 優先候補として扱う Wait か（許可イベントが 1 件以上）。
    /// </summary>
    /// <remarks>
    /// <see cref="ExtractDurableWaits"/> と同じ判定にし、cursor が execution_waits に無いノードを指さないようにする。
    /// </remarks>
    private static bool HasConfiguredWaitEvents(GraphNodeDto node) =>
        ResolveAllowedEvents(node).Count > 0;

    /// <summary>key 未指定・空白を空文字へ正規化する。</summary>
    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool TryParseGraph(string graphJson, out List<GraphNodeDto> nodes)
    {
        nodes = [];
        if (string.IsNullOrWhiteSpace(graphJson))
            return false;

        if (!JsonDeserialize.TryDeserialize(
                graphJson,
                JsonSerializerProfiles.CaseInsensitive,
                out ExecutionGraphSnapshotDto? dto)
            || dto?.Nodes is null)
            return false;

        nodes = dto.Nodes;
        return true;
    }

    private sealed record ActiveNodeSelection(string NodeId, string? WorkerId);

    private sealed class ExecutionGraphSnapshotDto
    {
        [JsonPropertyName("nodes")]
        public List<GraphNodeDto>? Nodes { get; set; }
    }

    private sealed class GraphNodeDto
    {
        [JsonPropertyName("nodeId")]
        public string? NodeId { get; set; }

        [JsonPropertyName("nodeName")]
        public string? NodeName { get; set; }

        [JsonPropertyName("nodeType")]
        public string? NodeType { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }

        [JsonPropertyName("waitKey")]
        public string? WaitKey { get; set; }

        [JsonPropertyName("allowedEvents")]
        public List<string>? AllowedEvents { get; set; }

        [JsonPropertyName("subscriptions")]
        public List<GraphSubscriptionDto>? Subscriptions { get; set; }
    }

    private sealed class GraphSubscriptionDto
    {
        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("resumeEventName")]
        public string? ResumeEventName { get; set; }
    }

    /// <summary>
    /// <see cref="HasDurableWaits"/> 専用。行は永続化されないため固定の空 Guid で足りる。
    /// </summary>
    private sealed class PresenceCheckIdGenerator : IIdGenerator
    {
        public static PresenceCheckIdGenerator Instance { get; } = new();

        public Guid NewSequentialGuid() => Guid.Empty;

        public Guid NewRandomGuid() => Guid.Empty;
    }
}
