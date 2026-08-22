using System.Text.Json;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Xunit;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>
/// <see cref="ExecutionViewMapper"/> が、DB 等に残る実行グラフ JSON（camelCase、ノードキーは <c>nodeId</c>）を
/// <see cref="ExecutionViewNodeDto"/>、<see cref="GraphPatchNodeDto"/>、および
/// <see cref="ExecutionWaitsResponse"/> に正しく射影することを検証する。
/// </summary>
public sealed class ExecutionViewMapperTests
{
    /// <summary>
    /// 永続化された camelCase の実行グラフ JSON を <see cref="ExecutionViewMapper.MapNodes"/> が解釈し、
    /// <see cref="ExecutionViewNodeDto.NodeId"/>（JSON の <c>nodeId</c>）、
    /// <see cref="ExecutionViewNodeDto.NodeName"/>、
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
                  "nodeName": "S1",
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
        Assert.Equal("nid-1", nodes[0].NodeId);
        Assert.Equal("S1", nodes[0].NodeName);
        Assert.Equal("w-9", nodes[0].WorkerId);
        Assert.True(nodes[0].Input.HasValue);
        var inputElement = nodes[0].Input!.Value;
        Assert.Equal(JsonValueKind.Object, inputElement.ValueKind);
        Assert.True(inputElement.TryGetProperty("seed", out var p) && p.GetBoolean());
    }

    /// <summary>
    /// グラフパッチ用 JSON を <see cref="ExecutionViewMapper.MapGraphPatchNodes"/> が解釈し、
    /// <see cref="GraphPatchNodeDto.NodeId"/>、
    /// <see cref="GraphPatchNodeDto.NodeName"/>、
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
                  "nodeName": "S1",
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
        Assert.Equal("nid-1", patch[0].NodeId);
        Assert.Equal("S1", patch[0].NodeName);
        Assert.Equal("w-9", patch[0].WorkerId);
    }

    /// <summary>
    /// Wait ノードの <c>allowedEvents</c> を Read model に透過し、空白を除去することを検証する。
    /// </summary>
    [Fact]
    public void MapNodes_maps_allowedEvents_from_wait_node()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "wait-1",
                  "nodeName": "ApproveTask",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null,
                  "attempt": 1,
                  "workerId": "w-1",
                  "waitKey": null,
                  "allowedEvents": [" approve ", "reject", ""],
                  "canceledByExecution": false
                }
              ]
            }
            """;

        // Act
        var nodes = ExecutionViewMapper.MapNodes(json);

        // Assert
        Assert.Single(nodes);
        Assert.Equal(["approve", "reject"], nodes[0].AllowedEvents);
        Assert.Null(nodes[0].WaitKey);
    }

    /// <summary>
    /// 未完了の複数イベント Wait が <see cref="ExecutionWaitItemDto"/> の
    /// <c>nodeId</c> / <c>nodeName</c> / <c>allowedEvents</c> になることを検証する。
    /// </summary>
    [Fact]
    public void MapActiveWaits_projects_multi_event_waiting_wait()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "wait-1",
                  "nodeName": "ApproveTask",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null,
                  "allowedEvents": ["approve", "reject"]
                }
              ]
            }
            """;

        // Act
        var response = ExecutionViewMapper.MapActiveWaits(json);

        // Assert
        Assert.Single(response.Waits);
        Assert.Equal("wait-1", response.Waits[0].NodeId);
        Assert.Equal("ApproveTask", response.Waits[0].NodeName);
        Assert.Equal(["approve", "reject"], response.Waits[0].AllowedEvents);
    }

    /// <summary>
    /// 単一イベント Wait は <c>allowedEvents</c> に 1 件だけ入り、<c>waitKey</c> キーは応答に出ないことを検証する。
    /// </summary>
    [Fact]
    public void MapActiveWaits_uses_allowedEvents_for_single_event_wait()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "wait-1",
                  "nodeName": "Hold",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null,
                  "waitKey": "go",
                  "allowedEvents": ["go"]
                }
              ]
            }
            """;

        // Act
        var response = ExecutionViewMapper.MapActiveWaits(json);
        var jsonText = JsonSerializer.Serialize(response);

        // Assert
        Assert.Single(response.Waits);
        Assert.Equal(["go"], response.Waits[0].AllowedEvents);
        Assert.DoesNotContain("waitKey", jsonText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 完了済み Wait と Task / Start は未完了 Wait 一覧に含まれないことを検証する。
    /// </summary>
    [Fact]
    public void MapActiveWaits_excludes_completed_wait_and_non_wait_nodes()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "start-1",
                  "nodeName": "Start",
                  "nodeType": "Start",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": "2020-01-01T00:00:01Z",
                  "fact": "Completed"
                },
                {
                  "nodeId": "task-1",
                  "nodeName": "Work",
                  "nodeType": "Task",
                  "startedAt": "2020-01-01T00:00:01Z",
                  "completedAt": null,
                  "fact": null
                },
                {
                  "nodeId": "wait-done",
                  "nodeName": "DoneWait",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:02Z",
                  "completedAt": "2020-01-01T00:00:03Z",
                  "fact": "Completed",
                  "allowedEvents": ["go"]
                },
                {
                  "nodeId": "wait-open",
                  "nodeName": "OpenWait",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:04Z",
                  "completedAt": null,
                  "fact": null,
                  "allowedEvents": ["resume"]
                }
              ]
            }
            """;

        // Act
        var response = ExecutionViewMapper.MapActiveWaits(json);

        // Assert
        Assert.Single(response.Waits);
        Assert.Equal("wait-open", response.Waits[0].NodeId);
        Assert.Equal(["resume"], response.Waits[0].AllowedEvents);
    }

    /// <summary>
    /// Wait が無いグラフ、空オブジェクト、空 nodes は空配列になることを検証する。
    /// </summary>
    [Fact]
    public void MapActiveWaits_returns_empty_when_no_active_waits()
    {
        // Arrange / Act / Assert
        Assert.Empty(ExecutionViewMapper.MapActiveWaits("""{"nodes":[]}""").Waits);
        Assert.Empty(ExecutionViewMapper.MapActiveWaits("{}").Waits);
        Assert.Empty(ExecutionViewMapper.MapActiveWaits("").Waits);
    }

    /// <summary>
    /// allowedEvents 欠落または空の未完了 Wait は空配列を返し、null にしないことを検証する。
    /// </summary>
    [Fact]
    public void MapActiveWaits_normalizes_missing_allowedEvents_to_empty_array()
    {
        // Arrange
        const string json =
            """
            {
              "nodes": [
                {
                  "nodeId": "wait-1",
                  "nodeName": "Hold",
                  "nodeType": "Wait",
                  "startedAt": "2020-01-01T00:00:00Z",
                  "completedAt": null,
                  "fact": null
                }
              ]
            }
            """;

        // Act
        var response = ExecutionViewMapper.MapActiveWaits(json);

        // Assert
        Assert.Single(response.Waits);
        Assert.NotNull(response.Waits[0].AllowedEvents);
        Assert.Empty(response.Waits[0].AllowedEvents);
    }
}
