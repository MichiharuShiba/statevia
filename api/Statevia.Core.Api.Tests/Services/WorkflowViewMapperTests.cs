using System.Text.Json;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Services;
using Xunit;

namespace Statevia.Core.Api.Tests.Services;

/// <summary>
/// <see cref="WorkflowViewMapper"/> が、DB 等に残る実行グラフ JSON（camelCase、ノードキーは <c>nodeId</c>）を
/// <see cref="WorkflowViewNodeDto"/> および <see cref="GraphPatchNodeDto"/> に正しく射影することを検証する。
/// </summary>
public sealed class WorkflowViewMapperTests
{
    /// <summary>
    /// 永続化された camelCase の実行グラフ JSON を <see cref="WorkflowViewMapper.MapNodes"/> が解釈し、
    /// <see cref="WorkflowViewNodeDto.ExecutionNodeId"/>（JSON の <c>nodeId</c>）、
    /// <see cref="WorkflowViewNodeDto.StateName"/>、
    /// <see cref="WorkflowViewNodeDto.WorkerId"/>、
    /// <see cref="WorkflowViewNodeDto.Input"/> が取り込まれること。
    /// </summary>
    [Fact]
    public void MapNodes_maps_stateName_workerId_and_input_from_camelCase_json()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "nid-1",
                  "stateName": "S1",
                  "nodeType": "Task",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null,
                  "input": { "seed": true },
                  "attempt": 1,
                  "workerId": "w-9",
                  "waitKey": null,
                  "canceledByExecution": false
                }
              ]
            }
            """;

        // Act
        var nodes = WorkflowViewMapper.MapNodes(json);

        // Assert
        Assert.Single(nodes);
        Assert.Equal("nid-1", nodes[0].ExecutionNodeId);
        Assert.Equal("S1", nodes[0].StateName);
        Assert.Equal("w-9", nodes[0].WorkerId);
        Assert.True(nodes[0].Input.HasValue);
        var inputElement = nodes[0].Input!.Value;
        Assert.Equal(JsonValueKind.Object, inputElement.ValueKind);
        Assert.True(inputElement.TryGetProperty("seed", out var p) && p.GetBoolean());
    }

    /// <summary>
    /// グラフパッチ用 JSON を <see cref="WorkflowViewMapper.MapGraphPatchNodes"/> が解釈し、
    /// <see cref="GraphPatchNodeDto.ExecutionNodeId"/>、
    /// <see cref="GraphPatchNodeDto.StateName"/>、
    /// <see cref="GraphPatchNodeDto.WorkerId"/> が取り込まれること。
    /// </summary>
    [Fact]
    public void MapGraphPatchNodes_includes_stateName_and_workerId()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "nid-1",
                  "stateName": "S1",
                  "nodeType": "Task",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null,
                  "attempt": 1,
                  "workerId": "w-9",
                  "waitKey": null,
                  "canceledByExecution": false
                }
              ]
            }
            """;

        // Act
        var patch = WorkflowViewMapper.MapGraphPatchNodes(json);

        // Assert
        Assert.Single(patch);
        Assert.Equal("nid-1", patch[0].ExecutionNodeId);
        Assert.Equal("S1", patch[0].StateName);
        Assert.Equal("w-9", patch[0].WorkerId);
    }
}
