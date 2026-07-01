using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Service.Api.Abstractions.Persistence;
using Statevia.Service.Api.Infrastructure;
using Statevia.Service.Api.Persistence;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Services;

/// <summary>
/// execution_cursors / execution_waits を execution 投影更新と同一 tx で同期する。
/// cursor は operational projection。read-model（executions / execution_graph_snapshots）の正本ではない。
/// durable wait は Engine Wait ノード（EventWait）のみ永続化する。
/// </summary>
internal static class ExecutionOperationalProjectionSync
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 投影フラッシュと同一トランザクション内で cursor / durable wait を同期する。
    /// Publish / Resume 時は <see cref="ExecutionOperationalProjectionSyncRequest.ResumeTokenToClear"/> で該当 wait を先行削除する。
    /// </summary>
    public static async Task SyncAsync(
        ICoreUnitOfWork uow,
        IExecutionCursorRepository cursors,
        IExecutionWaitRepository waits,
        ExecutionOperationalProjectionSyncRequest request,
        CancellationToken ct)
    {
        // resume_token 一致行を削除
        if (!string.IsNullOrWhiteSpace(request.ResumeTokenToClear))
        {
            await waits.DeleteByResumeTokenAsync(uow, request.ExecutionId, request.ResumeTokenToClear, ct)
                .ConfigureAwait(false);
        }

        // 終了状態の場合は cursor / wait を削除
        if (IsTerminalStatus(request.Status))
        {
            await cursors.DeleteAsync(uow, request.ExecutionId, ct).ConfigureAwait(false);
            await waits.ReplaceWaitsAsync(uow, request.ExecutionId, Array.Empty<ExecutionWaitRow>(), ct)
                .ConfigureAwait(false);
            return;
        }

        // cursor を更新
        var now = DateTime.UtcNow;
        var activeNode = SelectActiveNode(request.GraphJson, request.Snapshot);
        await cursors.UpsertAsync(
            uow,
            new ExecutionCursorRow
            {
                ExecutionId = request.ExecutionId,
                TenantId = request.TenantId,
                CurrentNodeId = activeNode?.NodeId,
                CurrentRuntimeId = null, // Note: 現時点では未使用
                CurrentWorkerId = activeNode?.WorkerId,
                State = request.Status,
                UpdatedAt = now
            },
            ct).ConfigureAwait(false);

        // durable wait を更新
        var durableWaits = ExtractDurableWaits(request.ExecutionId, request.GraphJson, now);
        await waits.ReplaceWaitsAsync(uow, request.ExecutionId, durableWaits, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 終了状態かどうかを判定する。
    /// </summary>
    private static bool IsTerminalStatus(string status) =>
        status is "Completed" or "Cancelled" or "Failed";

    /// <summary>
    /// アクティブノードを選択する。
    /// </summary>
    private static ActiveNodeSelection? SelectActiveNode(string graphJson, ExecutionSnapshot? snapshot)
    {
        // グラフ JSON を解析
        if (!TryParseGraph(graphJson, out var nodes) || nodes.Count == 0)
            return null;

        // 実行中ノードを取得
        var runningNodes = nodes
            .Where(n => n.CompletedAt is null && !string.IsNullOrWhiteSpace(n.NodeId))
            .ToList();
        if (runningNodes.Count == 0)
            return null;

        // Wait ノードを取得
        var waitCandidates = runningNodes
            .Where(n =>
                string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(n.WaitKey))
            .OrderByDescending(n => n.StartedAt)
            .ToList();
        
        // Wait ノードが存在する場合はそれを選択
        if (waitCandidates.Count > 0)
        {
            var selected = waitCandidates[0];
            return new ActiveNodeSelection(selected.NodeId!, selected.WorkerId);
        }

        // スナップショットからアクティブノードを取得
        if (snapshot?.ActiveStates is { Count: > 0 })
        {
            var activeStateSet = snapshot.ActiveStates.ToHashSet(StringComparer.Ordinal);
            var activeStateNode = runningNodes
                .Where(n => !string.IsNullOrWhiteSpace(n.StateName) && activeStateSet.Contains(n.StateName!))
                .OrderByDescending(n => n.StartedAt)
                .FirstOrDefault();
            if (activeStateNode is not null)
                return new ActiveNodeSelection(activeStateNode.NodeId!, activeStateNode.WorkerId);
        }

        // 実行中ノードが存在しない場合は最新のノードを選択
        var fallback = runningNodes.OrderByDescending(n => n.StartedAt).First();
        return new ActiveNodeSelection(fallback.NodeId!, fallback.WorkerId);
    }

    /// <summary>
    /// 仕様で durable と定義された EventWait のみ抽出する。
    /// 条件: nodeType=Wait、completedAt=null、waitKey あり。
    /// </summary>
    private static IReadOnlyList<ExecutionWaitRow> ExtractDurableWaits(
        Guid executionId,
        string graphJson,
        DateTime nowUtc)
    {
        // グラフ JSON を解析
        if (!TryParseGraph(graphJson, out var nodes))
            return Array.Empty<ExecutionWaitRow>();

        // durable と定義された EventWait のみ抽出
        return nodes
            .Where(n =>
                n.CompletedAt is null
                && !string.IsNullOrWhiteSpace(n.NodeId)
                && !string.IsNullOrWhiteSpace(n.WaitKey)
                && string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase))
            .Select(n => new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = n.NodeId!,
                WaitKind = ExecutionWaitKind.EventWait,
                ResumeToken = n.WaitKey!,
                ExpiresAt = null, // Note: 現時点では未使用
                CreatedAt = nowUtc
            })
            .ToList();
    }

    /// <summary>
    /// グラフ JSON を解析する。
    /// </summary>
    private static bool TryParseGraph(string graphJson, out List<GraphNodeDto> nodes)
    {
        nodes = [];
        if (string.IsNullOrWhiteSpace(graphJson))
            return false;

        if (!JsonDeserialize.TryDeserialize(
                graphJson,
                CaseInsensitiveJsonSerializerOptions,
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

        [JsonPropertyName("stateName")]
        public string? StateName { get; set; }

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
    }
}
