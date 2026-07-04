using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Application.Infrastructure;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行グラフ JSON と DB 行から UI 向け ExecutionView / Graph パッチを組み立てる。
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

        if (!JsonDeserialize.TryDeserialize(graphJson, JsonDeserialize.CaseInsensitiveDeserializeOptions, out ExecutionGraphSnapshotDto? dto))
            return Array.Empty<ExecutionViewNodeDto>();

        if (dto?.Nodes is null || dto.Nodes.Count == 0)
            return Array.Empty<ExecutionViewNodeDto>();

        var list = new List<ExecutionViewNodeDto>(dto.Nodes.Count);
        foreach (var n in dto.Nodes)
        {
            var nodeStatus = MapNodeStatus(n);
            var canceledByExecution = n.CanceledByExecution
                ?? string.Equals(n.Fact, "Cancelled", StringComparison.OrdinalIgnoreCase);
            var nodeType = ResolveNodeType(n);

            list.Add(new ExecutionViewNodeDto
            {
                ExecutionNodeId = n.NodeId ?? string.Empty,
                StateName = n.StateName ?? string.Empty,
                NodeType = nodeType,
                Status = nodeStatus,
                Attempt = n.Attempt ?? 1,
                WorkerId = n.WorkerId,
                WaitKey = n.WaitKey,
                CanceledByExecution = canceledByExecution,
                Input = n.Input,
                Output = n.Output,
                ConditionRouting = n.ConditionRouting
            });
        }

        return list;
    }

    public static IReadOnlyList<GraphPatchNodeDto> MapGraphPatchNodes(string graphJson)
    {
        var nodes = MapNodes(graphJson);
        return nodes.Select(n => new GraphPatchNodeDto
        {
            ExecutionNodeId = n.ExecutionNodeId,
            StateName = string.IsNullOrWhiteSpace(n.StateName) ? null : n.StateName,
            Status = n.Status,
            Attempt = n.Attempt,
            WorkerId = n.WorkerId,
            WaitKey = n.WaitKey,
            CanceledByExecution = n.CanceledByExecution
        }).ToList();
    }

    private static string MapNodeStatus(ExecutionNodeDto node)
    {
        if (node.CompletedAt is null)
            return "RUNNING";

        return node.Fact switch
        {
            "Completed" => "SUCCEEDED",
            "Failed" => "FAILED",
            "Cancelled" => "CANCELED",
            "Joined" => "SUCCEEDED",
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

        [JsonPropertyName("stateName")]
        public string? StateName { get; set; }

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

        [JsonPropertyName("canceledByExecution")]
        public bool? CanceledByExecution { get; set; }

        [JsonPropertyName("conditionRouting")]
        public JsonElement? ConditionRouting { get; set; }
    }
}
