using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>
/// 実行グラフ JSON と DB 行から UI 向け WorkflowView / Graph パッチを組み立てる。
/// </summary>
internal static class WorkflowViewMapper
{
    private static readonly JsonSerializerOptions s_graphJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WorkflowViewDto BuildWorkflowView(
        WorkflowRow workflow,
        string graphJson,
        string displayId,
        string graphIdDisplay)
    {
        return new WorkflowViewDto
        {
            DisplayId = displayId,
            ResourceId = workflow.WorkflowId.ToString("D"),
            GraphId = graphIdDisplay,
            Status = workflow.Status,
            StartedAt = workflow.StartedAt,
            UpdatedAt = workflow.UpdatedAt,
            CancelRequested = workflow.CancelRequested,
            RestartLost = workflow.RestartLost,
            Nodes = MapNodes(graphJson)
        };
    }

    public static IReadOnlyList<WorkflowViewNodeDto> MapNodes(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return Array.Empty<WorkflowViewNodeDto>();

        ExecutionGraphSnapshotDto? dto;
        try
        {
            // 既存DBに残る PascalCase スナップショットとの後方互換を維持する。
            dto = JsonSerializer.Deserialize<ExecutionGraphSnapshotDto>(graphJson, s_graphJsonOptions);
        }
        catch
        {
            return Array.Empty<WorkflowViewNodeDto>();
        }

        if (dto?.Nodes is null || dto.Nodes.Count == 0)
            return Array.Empty<WorkflowViewNodeDto>();

        var list = new List<WorkflowViewNodeDto>(dto.Nodes.Count);
        foreach (var n in dto.Nodes)
        {
            var nodeStatus = MapNodeStatus(n);
            var canceledByExecution = string.Equals(n.Fact, "Cancelled", StringComparison.OrdinalIgnoreCase);

            list.Add(new WorkflowViewNodeDto
            {
                NodeId = n.NodeId ?? string.Empty,
                NodeType = string.IsNullOrEmpty(n.StateName) ? "Task" : n.StateName!,
                Status = nodeStatus,
                Attempt = 1,
                WorkerId = null,
                WaitKey = null,
                CanceledByExecution = canceledByExecution,
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
            NodeId = n.NodeId,
            Status = n.Status,
            Attempt = n.Attempt,
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

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("fact")]
        public string? Fact { get; set; }

        [JsonPropertyName("conditionRouting")]
        public JsonElement? ConditionRouting { get; set; }
    }
}
