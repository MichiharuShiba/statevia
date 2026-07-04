using System.Text.Json;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Xunit;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>
/// <see cref="ExecutionViewMapper"/> が、DB 等に残る実行グラフ JSON（camelCase、ノードキーは <c>nodeId</c>）を
/// <see cref="ExecutionViewNodeDto"/> および <see cref="GraphPatchNodeDto"/> に正しく射影することを検証する。
/// </summary>
public sealed class ExecutionViewMapperTests
{
    /// <summary>
    /// 永続化された camelCase の実行グラフ JSON を <see cref="ExecutionViewMapper.MapNodes"/> が解釈し、
    /// <see cref="ExecutionViewNodeDto.ExecutionNodeId"/>（JSON の <c>nodeId</c>）、
    /// <see cref="ExecutionViewNodeDto.StateName"/>、
    /// <see cref="ExecutionViewNodeDto.WorkerId"/>、
    /// <see cref="ExecutionViewNodeDto.Input"/> が取り込まれること。
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
        var nodes = ExecutionViewMapper.MapNodes(json);

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
    /// グラフパッチ用 JSON を <see cref="ExecutionViewMapper.MapGraphPatchNodes"/> が解釈し、
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
        var patch = ExecutionViewMapper.MapGraphPatchNodes(json);

        // Assert
        Assert.Single(patch);
        Assert.Equal("nid-1", patch[0].ExecutionNodeId);
        Assert.Equal("S1", patch[0].StateName);
        Assert.Equal("w-9", patch[0].WorkerId);
    }
}
