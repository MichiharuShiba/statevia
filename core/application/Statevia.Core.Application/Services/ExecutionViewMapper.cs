using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行グラフ JSON と DB 行から UI 向け ExecutionView / Graph パッチ、および未完了 Wait の再開キー一覧を組み立てる。
/// </summary>
internal static class ExecutionViewMapper
{
    public static ExecutionViewDto BuildExecutionView(
        ExecutionRow execution,
        string graphJson,
        string displayId,
        string graphIdDisplay)
    {
        return new ExecutionViewDto
        {
            DisplayId = displayId,
            ResourceId = execution.ExecutionId.ToString("D"),
            GraphId = graphIdDisplay,
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            UpdatedAt = execution.UpdatedAt,
            CancelRequested = execution.CancelRequested,
            RestartLost = execution.RestartLost,
            Nodes = MapNodes(graphJson)
        };
    }

    public static IReadOnlyList<ExecutionViewNodeDto> MapNodes(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return Array.Empty<ExecutionViewNodeDto>();

        if (!JsonDeserialize.TryDeserialize(graphJson, JsonSerializerProfiles.CaseInsensitive, out ExecutionGraphSnapshotDto? dto))
            return Array.Empty<ExecutionViewNodeDto>();

        if (dto?.Nodes is null || dto.Nodes.Count == 0)
            return Array.Empty<ExecutionViewNodeDto>();

        var list = new List<ExecutionViewNodeDto>(dto.Nodes.Count);
        foreach (var n in dto.Nodes)
        {
            var nodeStatus = MapNodeStatus(n);
            var canceledByExecution = n.CanceledByExecution
                ?? string.Equals(n.Fact, Fact.Cancelled, StringComparison.OrdinalIgnoreCase);
            var nodeType = ResolveNodeType(n);

            list.Add(new ExecutionViewNodeDto
            {
                NodeId = n.NodeId ?? string.Empty,
                NodeName = n.NodeName ?? string.Empty,
                NodeType = nodeType,
                Status = nodeStatus,
                Attempt = n.Attempt ?? 1,
                WorkerId = n.WorkerId,
                WaitKey = n.WaitKey,
                AllowedEvents = NormalizeAllowedEvents(n.AllowedEvents),
                CanceledByExecution = canceledByExecution,
                Input = n.Input,
                Output = n.Output,
                ConditionRouting = n.ConditionRouting
            });
        }

        return list;
    }

    /// <summary>
    /// graph スナップショット JSON から未完了 Wait だけを再開キーとして射影する。
    /// </summary>
    /// <param name="graphJson"><see cref="IExecutionService.GetGraphJsonAsync"/> と同じグラフ JSON。</param>
    /// <returns>WAITING かつ NodeType が Wait の要素。順序は <c>nodes</c> 配列順。0 件でも空の <see cref="ExecutionWaitsResponse"/>。</returns>
    /// <remarks>
    /// IO-14: <c>input</c> / <c>output</c> は載せない。<c>allowedEvents</c> が null のときは空配列にする。
    /// </remarks>
    public static ExecutionWaitsResponse MapActiveWaits(string graphJson)
    {
        var waits = MapNodes(graphJson)
            .Where(node =>
                string.Equals(node.Status, "WAITING", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.NodeType, "Wait", StringComparison.OrdinalIgnoreCase))
            .Select(node => new ExecutionWaitItemDto
            {
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                AllowedEvents = node.AllowedEvents is { Count: > 0 }
                    ? node.AllowedEvents
                    : Array.Empty<string>()
            })
            .ToList();

        return new ExecutionWaitsResponse { Waits = waits };
    }

    public static IReadOnlyList<GraphPatchNodeDto> MapGraphPatchNodes(string graphJson)
    {
        var nodes = MapNodes(graphJson);
        return nodes.Select(n => new GraphPatchNodeDto
        {
            NodeId = n.NodeId,
            NodeName = string.IsNullOrWhiteSpace(n.NodeName) ? null : n.NodeName,
            Status = n.Status,
            Attempt = n.Attempt,
            WorkerId = n.WorkerId,
            WaitKey = n.WaitKey,
            AllowedEvents = n.AllowedEvents,
            CanceledByExecution = n.CanceledByExecution
        }).ToList();
    }

    /// <summary>空・空白のみを除き、前後空白を Trim した許可イベント一覧を返す（空なら null）。</summary>
    private static List<string>? NormalizeAllowedEvents(List<string>? allowedEvents)
    {
        if (allowedEvents is null || allowedEvents.Count == 0)
            return null;

        var normalized = allowedEvents
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0 ? null : normalized;
    }

    /// <summary>
    /// グラフノードの UI 向け status を解決する。
    /// 未完了 Wait は WAITING（Resume 可否と仕様の NodeStatus に合わせる）。
    /// </summary>
    private static string MapNodeStatus(ExecutionNodeDto node)
    {
        if (node.CompletedAt is null)
        {
            return string.Equals(node.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                ? "WAITING"
                : "RUNNING";
        }

        return node.Fact switch
        {
            Fact.Completed => "SUCCEEDED",
            Fact.Failed => "FAILED",
            Fact.Cancelled => "CANCELED",
            Fact.Joined => "SUCCEEDED",
            _ => "SUCCEEDED"
        };
    }

    private static string ResolveNodeType(ExecutionNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.NodeType))
            return node.NodeType!;
        return "Task";
    }

    private sealed class ExecutionGraphSnapshotDto
    {
        [JsonPropertyName("nodes")]
        public List<ExecutionNodeDto>? Nodes { get; set; }
    }

    private sealed class ExecutionNodeDto
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

        [JsonPropertyName("fact")]
        public string? Fact { get; set; }

        [JsonPropertyName("input")]
        public JsonElement? Input { get; set; }

        [JsonPropertyName("output")]
        public JsonElement? Output { get; set; }

        [JsonPropertyName("attempt")]
        public int? Attempt { get; set; }

        [JsonPropertyName("workerId")]
        public string? WorkerId { get; set; }

        [JsonPropertyName("waitKey")]
        public string? WaitKey { get; set; }

        [JsonPropertyName("allowedEvents")]
        public List<string>? AllowedEvents { get; set; }

        [JsonPropertyName("canceledByExecution")]
        public bool? CanceledByExecution { get; set; }

        [JsonPropertyName("conditionRouting")]
        public JsonElement? ConditionRouting { get; set; }
    }
}
