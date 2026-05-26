using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Services;

/// <summary>
/// GET …/stream 用の SSE。グラフ JSON の変化を <c>GraphUpdated</c> イベント相当の JSON で送出する。
/// </summary>
public sealed class ExecutionStreamService
{
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.Ordinal)
    {
        "Completed",
        "Cancelled",
        "Failed"
    };

    /// <summary>
    /// 投影グラフ取得のポーリング間隔（ミリ秒）。
    /// </summary>
    internal const int GraphPollingIntervalMilliseconds = 2000;

    private readonly IExecutionService _executions;
    private readonly IDisplayIdService _displayIds;

    /// <summary>
    /// <see cref="ExecutionStreamService"/> を生成する。
    /// </summary>
    /// <param name="executions">実行サービス。</param>
    /// <param name="displayIds">表示 ID 解決。</param>
    public ExecutionStreamService(IExecutionService executions, IDisplayIdService displayIds)
    {
        _executions = executions;
        _displayIds = displayIds;
    }

    /// <summary>
    /// Server-Sent Events としてグラフ JSON の変化を書き込む。
    /// </summary>
    /// <param name="response">HTTP レスポンス。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="idOrUuid">ワークフロー表示 ID または UUID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task WriteStreamAsync(HttpResponse response, string tenantId, string idOrUuid, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (ct.IsCancellationRequested)
            return;

        var uuid = await _displayIds.ResolveAsync("execution", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 接続開始時に tenant + execution の存在を一度だけ確認する。
        await _executions.EnsureExecutionExistsAsync(tenantId, uuid.Value, ct).ConfigureAwait(false);

        var displayId = await _displayIds.GetDisplayIdAsync(DisplayIdResourceTypes.Execution, idOrUuid, ct).ConfigureAwait(false) ?? idOrUuid;

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string? lastHash = null;

        while (!ct.IsCancellationRequested)
        {
            var snapshotResult = await TryGetSnapshotGraphJsonAsync(response, uuid.Value, ct).ConfigureAwait(false);
            if (!snapshotResult.ShouldContinue)
                return;
            if (snapshotResult.GraphJson is null)
                continue;
            var graphJson = snapshotResult.GraphJson;

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(graphJson)));
            if (!string.Equals(hash, lastHash, StringComparison.Ordinal))
            {
                lastHash = hash;
                if (!await ProcessGraphUpdateAsync(response, graphJson, displayId, jsonOpts, ct).ConfigureAwait(false))
                    return;
            }

            if (!await DelayNextPollAsync(response, ct).ConfigureAwait(false))
                return;
        }
    }

    private static bool IsStreamCancellation(HttpResponse response, CancellationToken ct) =>
        ct.IsCancellationRequested || response.HttpContext.RequestAborted.IsCancellationRequested;

    private static async Task<bool> DelayNextPollAsync(HttpResponse response, CancellationToken ct)
    {
        try
        {
            await Task.Delay(GraphPollingIntervalMilliseconds, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return false;
        }
    }

    private async Task<(bool ShouldContinue, string? GraphJson)> TryGetSnapshotGraphJsonAsync(HttpResponse response, Guid executionId, CancellationToken ct)
    {
        try
        {
            var snapshotGraphJson = await _executions.TryGetSnapshotGraphJsonByExecutionIdAsync(executionId, ct).ConfigureAwait(false);
            if (snapshotGraphJson is null)
            {
                // スナップショット行が消えた場合はストリームを終了する。
                return (false, null);
            }
            return (true, snapshotGraphJson);
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return (false, null);
        }
#pragma warning disable CA1031 // SSE ポーリング: DB／実行時の未取得例外でも接続維持のためポーリングを継続する
        catch (Exception)
        {
            var canContinue = await DelayNextPollAsync(response, ct).ConfigureAwait(false);
            return (canContinue, null);
        }
#pragma warning restore CA1031
    }

    private static async Task<bool> TryWriteAsync(HttpResponse response, string payload, CancellationToken ct)
    {
        try
        {
            await response.WriteAsync($"data: {payload}\n\n", ct).ConfigureAwait(false);
            await response.Body.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (IsStreamCancellation(response, ct))
        {
            return false;
        }
        catch (IOException) when (response.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<bool> ProcessGraphUpdateAsync(
        HttpResponse response,
        string graphJson,
        string displayId,
        JsonSerializerOptions jsonOpts,
        CancellationToken ct)
    {
        var patchNodes = ExecutionViewMapper.MapGraphPatchNodes(graphJson);
        var payload = JsonSerializer.Serialize(
            new
            {
                type = "GraphUpdated",
                executionId = displayId,
                patch = new { nodes = patchNodes }
            },
            jsonOpts);

        if (!await TryWriteAsync(response, payload, ct).ConfigureAwait(false))
            return false;

        if (IsTerminalSnapshotGraph(graphJson))
            return false;

        return true;
    }

    private static bool IsTerminalSnapshotGraph(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(graphJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("nodes", out var nodesElement) || nodesElement.ValueKind != JsonValueKind.Array)
                return false;

            var sinkNodeIds = GetSinkNodeIds(root, nodesElement);
            return nodesElement.EnumerateArray().Any(node =>
                IsTerminalNodeFact(node) || IsCompletedSinkNode(node, sinkNodeIds));
        }
        catch (JsonException)
        {
            // 不正 JSON は終端扱いにせず次回ポーリングで再評価する。
            return false;
        }
    }

    private static HashSet<string> GetSinkNodeIds(JsonElement root, JsonElement nodesElement)
    {
        var nodeIds = nodesElement
            .EnumerateArray()
            .Select(node => node.TryGetProperty("nodeId", out var idElement) ? idElement.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        if (!root.TryGetProperty("edges", out var edgesElement) || edgesElement.ValueKind != JsonValueKind.Array)
            return nodeIds;

        foreach (var edge in edgesElement.EnumerateArray())
        {
            if (!edge.TryGetProperty("from", out var fromElement))
                continue;
            var fromId = fromElement.GetString();
            if (string.IsNullOrWhiteSpace(fromId))
                continue;
            nodeIds.Remove(fromId);
        }

        return nodeIds;
    }

    private static bool IsTerminalNodeFact(JsonElement node)
    {
        var fact = node.TryGetProperty("fact", out var factElement) ? factElement.GetString() : null;
        return fact is not null && TerminalStatuses.Contains(fact);
    }

    private static bool IsCompletedSinkNode(JsonElement node, HashSet<string> sinkNodeIds)
    {
        if (!node.TryGetProperty("nodeId", out var nodeIdElement))
            return false;
        var nodeId = nodeIdElement.GetString();
        if (string.IsNullOrWhiteSpace(nodeId) || !sinkNodeIds.Contains(nodeId))
            return false;
        return node.TryGetProperty("completedAt", out var completedAtElement) && completedAtElement.ValueKind != JsonValueKind.Null;
    }
}
